using CorePDF.Conformance;
using CorePDF.Contents;
using CorePDF.Embeds;
using CorePDF.Pages;
using CorePDF.TypeFaces;
using CorePDF.Writing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace CorePDF
{
    /// <summary>
    /// The document object
    /// </summary>
    public class Document
    {
        private Catalog _catalog { get; set; } = new Catalog();
        private PageRoot _pageRoot { get; set; }
        private List<Font> _fonts { get; set; } = new List<Font>();

        /// <summary>
        /// Document properties
        /// </summary>
        public Properties Properties { get; set; } = new Properties();

        /// <summary>
        /// The pages of the document
        /// </summary>
        public List<Page> Pages { get; set; } = new List<Page>();

        /// <summary>
        /// Any headers or footers used in the document
        /// </summary>
        public List<HeaderFooter> HeadersFooters { get; set; } = new List<HeaderFooter>();

        /// <summary>
        /// Any images used in the document
        /// </summary>
        public List<ImageFile> Images { get; set; } = new List<ImageFile>();

        /// <summary>
        /// Any fonts to be embedded n the document
        /// </summary>
        public List<FontFile> FontFiles { get; set; } = new List<FontFile>();

        /// <summary>
        /// If the document content streams are compressed
        /// </summary>
        public bool CompressContent { get; set; } = false;

        /// <summary>
        /// The archival standard the document must conform to. When set to a PDF/A level,
        /// publishing will fail with an explanation if the document can't comply (eg: if text
        /// uses one of the standard fonts, which can't be embedded).
        /// </summary>
        public PdfConformance Conformance { get; set; } = PdfConformance.None;

        public Document()
        {
            _pageRoot = new PageRoot
            {
                Document = this
            };
        }

        public void EmbedFont(FontFile font)
        {
            FontFiles.Add(font);
        }

        /// <summary>
        /// Builds the list of fonts available to the document: the standard and barcode fonts
        /// plus one for each embedded font file. This is done at publish time, rather than in
        /// EmbedFont, so that font files added any other way (e.g. directly to FontFiles or by
        /// deserialising a document) are included, along with any changes made after embedding.
        /// </summary>
        private void BuildFontList()
        {
            _fonts = new List<Font>();
            _fonts.AddRange(Fonts.Styles());
            _fonts.AddRange(Fonts.Barcodes());

            foreach (var fontFile in FontFiles)
            {
                var ttf = new TrueTypeFont(fontFile.ReadData());
                if (ttf.EmbeddingRestricted)
                {
                    throw new InvalidOperationException($"The licence of the font '{fontFile.Name}' does not allow it to be embedded in documents.");
                }

                _fonts.Add(new EmbeddedFont(fontFile, ttf));
            }

            var fontCount = 0;
            foreach (var font in _fonts)
            {
                fontCount++;
                font.Id = string.Format("F{0}", fontCount);
            }
        }

        /// <summary>
        /// Called to produce the document stream. The stream is left open.
        /// </summary>
        /// <param name="baseStream"></param>
        public void Publish(Stream baseStream)
        {
            using (new InvariantCultureScope())
            using (var output = new PdfOutputStream(baseStream))
            using (var stream = new StreamWriter(output, new UTF8Encoding(false)))
            {
                stream.NewLine = "\n";

                // Lay out all the content first. This determines which fonts (and which
                // characters within them) are used, so only those need to be included.
                BuildFontList();
                EmbedFiles();
                AssignIds();
                PrepareStreams();
                CheckConformance();

                foreach (var page in Pages)
                {
                    page.CollectLinks();
                }

                var objectCount = AssignObjectNumbers();

                // PDF Header
                stream.WriteLine("%PDF-{0}", Conformance == PdfConformance.None ? "1.4" : "1.7");
                stream.WriteLine("%\x82\x82\x82\x82"); // needed to allow editors to know that this is a binary file

                // Call publish on all the child objects
                _catalog.Publish(_pageRoot, stream);

                foreach (var font in _fonts.Where(f => f.Used))
                {
                    font.Publish(stream);
                }

                foreach (var image in Images)
                {
                    image.Publish(stream);
                }

                _pageRoot.Publish(stream);

                foreach (var headerFooter in HeadersFooters)
                {
                    headerFooter.Publish(stream);
                }

                foreach (var page in Pages)
                {
                    foreach (var content in page.Contents)
                    {
                        content.Publish(stream);
                    }
                }

                foreach (var page in Pages)
                {
                    page.Publish(_fonts, stream);
                }

                foreach (var page in Pages)
                {
                    PublishLinks(page, stream);
                }

                //// Document attributes
                Properties.Publish(stream);

                if (_catalog.MetadataNumber > 0)
                {
                    // XMP metadata must not be compressed so it can be found by tools that don't understand PDF
                    new RawObject(_catalog.MetadataNumber, new Dictionary<string, dynamic>
                    {
                        { "/Type", "/Metadata" },
                        { "/Subtype", "/XML" }
                    }, Encoding.UTF8.GetBytes(Properties.ToXmp(Conformance))).Publish(stream);
                }

                if (_catalog.OutputProfileNumber > 0)
                {
                    new RawObject(_catalog.OutputProfileNumber, new Dictionary<string, dynamic>
                    {
                        { "/N", 3 }
                    }, SrgbIccProfile.Bytes, compress: true).Publish(stream);
                }

                stream.Flush();

                // the document id is a hash of its content, so publishing the same document
                // always gives the same result
                var id = PdfString.Hex(output.GetHashSoFar());

                // PDF Cross Reference
                var startXref = output.Position;
                stream.Write(CreateXRefTable(output, objectCount));

                // PDF Trailer
                var trailer = "trailer\n";
                trailer += string.Format("<</Size {0}\n", objectCount + 1);
                trailer += string.Format("/Root {0} 0 R\n", _catalog.ObjectNumber);
                trailer += string.Format("/Info {0} 0 R\n", Properties.ObjectNumber);
                trailer += string.Format("/ID [{0} {0}]\n", id);
                trailer += ">>\n";
                trailer += "startxref\n";
                trailer += startXref.ToString(CultureInfo.InvariantCulture) + "\n";
                trailer += "%%EOF";

                // PDF END
                stream.Write(trailer);

                stream.Flush();
            }
        }

        public virtual HeaderFooter GetHeaderFooter(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            return HeadersFooters.Find(hf => hf.Name.ToLower() == name.ToLower());
        }

        public virtual ImageFile GetImage(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            return Images.Find(i => i.Name.ToLower() == name.ToLower());
        }

        private void EmbedFiles()
        {
            foreach (var image in Images)
            {
                image.EmbedFile();
                image.Interpolate = Conformance == PdfConformance.None;
            }
        }

        private void PrepareStreams()
        {
            foreach (var image in Images)
            {
                image.PrepareStream();
            }

            foreach (var headerfooter in HeadersFooters)
            {
                 headerfooter.PrepareStream(_pageRoot, _fonts, CompressContent);
            }

            foreach (var page in Pages)
            {
                page.PrepareStream(_fonts, CompressContent);
            }
        }

        /// <summary>
        /// Makes sure the document can meet the requested conformance level
        /// </summary>
        private void CheckConformance()
        {
            if (Conformance == PdfConformance.None) return;

            var standardFonts = _fonts.Where(f => f.Used && !f.IsEmbedded).Select(f => f.FontName).ToList();
            if (standardFonts.Count > 0)
            {
                throw new InvalidOperationException(string.Format(
                    "PDF/A documents must embed every font they use, but the standard font(s) {0} cannot be embedded. " +
                    "Embed a TrueType font with Document.EmbedFont and use it for this text instead.",
                    string.Join(", ", standardFonts.Select(f => "'" + f + "'"))));
            }
        }

        /// <summary>
        /// Writes the link annotations for a page
        /// </summary>
        private void PublishLinks(Page page, StreamWriter stream)
        {
            for (var i = 0; i < page.Links.Count; i++)
            {
                var link = page.Links[i];
                new RawObject(page.FirstAnnotationNumber + i, new Dictionary<string, dynamic>
                {
                    { "/Type", "/Annot" },
                    { "/Subtype", "/Link" },
                    { "/Rect", string.Format(CultureInfo.InvariantCulture, "[{0} {1} {2} {3}]",
                        Math.Round(link.Left, 2), Math.Round(link.Bottom, 2), Math.Round(link.Right, 2), Math.Round(link.Top, 2)) },
                    { "/Border", "[0 0 0]" },
                    { "/F", 4 }, // print the annotation with the page (required by PDF/A)
                    { "/A", string.Format("<< /S /URI /URI {0} >>", PdfString.Uri(link.Uri)) }
                }).Publish(stream);
            }
        }

        /// <summary>
        /// Builds the cross reference table from the positions recorded as each object was written
        /// </summary>
        private static string CreateXRefTable(PdfOutputStream output, int objectCount)
        {
            // each entry must be exactly 20 bytes long, including the two byte end of line
            var result = new StringBuilder();
            result.Append("xref\n");
            result.AppendFormat("0 {0}\n", objectCount + 1);
            result.Append("0000000000 65535 f \n");

            for (var objectNumber = 1; objectNumber <= objectCount; objectNumber++)
            {
                if (!output.ObjectOffsets.TryGetValue(objectNumber, out var position))
                {
                    throw new InvalidOperationException($"Object number {objectNumber} was reserved but never written.");
                }

                result.AppendFormat("{0} 00000 n \n", position.ToString(CultureInfo.InvariantCulture).PadLeft(10, '0'));
            }

            return result.ToString();
        }

        /// <summary>
        /// Gives the content the identifiers used to refer to fonts and images within the
        /// page content, and puts the content into drawing order.
        /// </summary>
        private void AssignIds()
        {
            // need to sort the content elements on each page by z-Index (from 0 to n)
            // this is because the PDF renders the objects in the order they are defined
            foreach (var page in Pages)
            {
                SortByZIndex(page.Contents);
            }

            foreach (var headerfooter in HeadersFooters)
            {
                SortByZIndex(headerfooter.Contents);
            }

            _catalog.Id = "C1";
            _pageRoot.Id = "R1";
            Properties.Id = "N1";

            var imageCount = 0;
            foreach (var image in Images)
            {
                imageCount++;
                image.Id = string.Format("I{0}", imageCount);

                if (image.MaskData != null)
                {
                    imageCount++;
                    image.MaskData.Id = string.Format("I{0}", imageCount);
                }
            }

            var contentCount = 0;
            var seen = new HashSet<Content>(ReferenceEqualityComparer.Instance);
            foreach (var content in HeadersFooters.SelectMany(hf => hf.Contents).Concat(Pages.SelectMany(p => p.Contents)))
            {
                if (!seen.Add(content))
                {
                    // each content object is written once, as part of the page it is on
                    throw new InvalidOperationException(
                        $"The same {content.GetType().Name} object has been added to more than one page (or more than once). " +
                        "Use a separate object for each page, or put content that repeats on every page in a header or footer.");
                }

                contentCount++;
                content.Id = string.Format("C{0}", contentCount);
            }

            var pageCount = 0;
            foreach (var page in Pages)
            {
                // Attach the page root to each page
                page.PageRoot = _pageRoot;

                pageCount++;
                page.Id = string.Format("P{0}", pageCount);
            }
        }

        private static void SortByZIndex(List<Content> contents)
        {
            contents.Sort((Content x, Content y) =>
            {
                if (x.ZIndex == 0 && y.ZIndex == 0) return 0;
                else if (x.ZIndex == 0) return -1;
                else if (y.ZIndex == 0) return 1;
                else return x.ZIndex.CompareTo(y.ZIndex);
            });
        }

        /// <summary>
        /// Numbers each object that will be written to the document. Returns the number of objects.
        /// </summary>
        private int AssignObjectNumbers()
        {
            var next = 1;

            // Catalog is always the first object
            _catalog.ObjectNumber = next++;

            foreach (var font in _fonts.Where(f => f.Used))
            {
                next = font.AssignObjectNumbers(next);
            }

            foreach (var image in Images.Where(i => i.IsPublished))
            {
                image.ObjectNumber = next++;

                if (image.MaskData != null)
                {
                    image.MaskData.ObjectNumber = next++;
                }
            }

            _pageRoot.ObjectNumber = next++;

            foreach (var content in HeadersFooters.SelectMany(hf => hf.Contents).Concat(Pages.SelectMany(p => p.Contents)))
            {
                content.ObjectNumber = next++;
            }

            foreach (var page in Pages)
            {
                page.ObjectNumber = next++;
            }

            foreach (var page in Pages)
            {
                page.FirstAnnotationNumber = next;
                next += page.Links.Count;
            }

            // Document Info properties
            Properties.ObjectNumber = next++;

            _catalog.MetadataNumber = 0;
            _catalog.OutputProfileNumber = 0;
            if (Conformance != PdfConformance.None)
            {
                _catalog.MetadataNumber = next++;
                _catalog.OutputProfileNumber = next++;
            }

            return next - 1;
        }
    }

}
