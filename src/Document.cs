﻿using CorePDF.Contents;
using CorePDF.Embeds;
using CorePDF.Pages;
using CorePDF.TypeFaces;
using System;
using System.Collections.Generic;
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
 
        public Document()
        {
            _pageRoot = new PageRoot
            {
                Document = this
            };
            _fonts.AddRange(Fonts.Styles());
            _fonts.AddRange(Fonts.Barcodes());
        }

        public void EmbedFont(FontFile font)
        {
            _fonts.Add(new Font
            {
                FontName = font.Name,
                BaseFont = font.BaseFont,
                Italic = font.Italic,
                Bold = font.Bold,
                Type = font.Type.ToString(),
                Metrics = font.Widths
            });

            FontFiles.Add(font);

        }

        /// <summary>
        /// Called to produce the document stream
        /// </summary>
        /// <param name="baseStream"></param>
        public void Publish(Stream baseStream)
        {
            using (StreamWriter stream = new StreamWriter(baseStream, new UTF8Encoding(false)))
            {
                stream.NewLine = "\n";

                // PDF Header
                stream.WriteLine("%PDF-{0}", "1.4"); // the version of PDF 
                stream.WriteLine("%\x82\x82\x82\x82"); // needed to allow editors to know that this is a binary file

                // get the data for the embedded files
                EmbedFiles();

                // place all the document objects into postion
                var objects = PositionObjects();

                // then get all the data ready for the document
                PrepareStreams();

                // Call publish on all the child objects
                _catalog.Publish(_pageRoot, stream);

                foreach (var font in _fonts)
                {
                    font.Publish(stream, FontFiles);
                }

                foreach (var font in FontFiles)
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

                //// Document attributes
                Properties.Publish(stream);

                // PDF Cross Reference
                var startXref = stream.BaseStream.Position;
                var xref = "xref\n";
                xref += string.Format("0 {0}\n", objects + 1);
                xref += CreateXRefTable();

                stream.Write(xref);

                // PDF Trailer
                var trailer = "trailer\n";
                trailer += string.Format("<</Size {0}\n", objects + 1);
                trailer += "/Root 1 0 R\n";

                if (Properties.ObjectNumber > 0)
                {
                    trailer += string.Format("/Info {0} 0 R\n", Properties.ObjectNumber);
                }
                trailer += ">>\n";
                trailer += "startxref\n";
                trailer += startXref.ToString() + "\n";
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
            }

            foreach (var font in FontFiles)
            {
                font.EmbedFile();
            }
        }

        private void PrepareStreams()
        {
            foreach (var image in Images)
            {
                image.PrepareStream();
            }

            foreach (var font in FontFiles)
            {
                font.PrepareStream(CompressContent);
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
        /// This needs to be processed in the same order as the object position method below.
        /// </summary>
        /// <returns></returns>
        private string CreateXRefTable()
        {
            var result = "0000000000 65535 f\n";
            result += string.Format("{0} 00000 n\n", _catalog.BytePosition.ToString().PadLeft(10, '0'));

            foreach (var font in _fonts)
            {
                result += string.Format("{0} 00000 n\n", font.BytePosition.ToString().PadLeft(10, '0'));
            }

            foreach (var font in FontFiles)
            {
                result += string.Format("{0} 00000 n\n", font.BytePosition.ToString().PadLeft(10, '0'));
                result += string.Format("{0} 00000 n\n", font.FileBytePosition.ToString().PadLeft(10, '0'));
            }

            foreach (var image in Images)
            {
                result += string.Format("{0} 00000 n\n", image.BytePosition.ToString().PadLeft(10, '0'));
            }

            result += string.Format("{0} 00000 n\n", _pageRoot.BytePosition.ToString().PadLeft(10, '0'));

            foreach (var headerfooter in HeadersFooters)
            {
                foreach (var content in headerfooter.Contents)
                {
                    result += string.Format("{0} 00000 n\n", content.BytePosition.ToString().PadLeft(10, '0'));
                }
            }

            foreach (var page in Pages)
            {
                foreach (var content in page.Contents)
                {
                    result += string.Format("{0} 00000 n\n", content.BytePosition.ToString().PadLeft(10, '0'));
                }
            }

            foreach (var page in Pages)
            {
                result += string.Format("{0} 00000 n\n", page.BytePosition.ToString().PadLeft(10, '0'));
            }

            result += string.Format("{0} 00000 n\n", Properties.BytePosition.ToString().PadLeft(10, '0'));

            return result;
        }

        private int PositionObjects()
        {
            // need to sort the content elements on each page by z-Index (from 0 to n)
            // this is because the PDF renders the objects in the order they are defined
            foreach (var page in Pages)
            {
                page.Contents.Sort((Content x, Content y) =>
                {
                    if (x.ZIndex == 0 && y.ZIndex == 0) return 0;
                    else if (x.ZIndex == 0) return -1;
                    else if (y.ZIndex == 0) return 1;
                    else return x.ZIndex.CompareTo(y.ZIndex);
                });
            }

            foreach (var headerfooter in HeadersFooters)
            {
                headerfooter.Contents.Sort((Content x, Content y) =>
                {
                    if (x.ZIndex == 0 && y.ZIndex == 0) return 0;
                    else if (x.ZIndex == 0) return -1;
                    else if (y.ZIndex == 0) return 1;
                    else return x.ZIndex.CompareTo(y.ZIndex);
                });
            }

            var objectCount = 0;
            var fontCount = 0;
            var imageCount = 0;
            var contentCount = 0;
            var pageCount = 0;

            // Catalog is always the first object
            objectCount++;
            _catalog.Id = "C1";
            _catalog.ObjectNumber = objectCount;

            foreach (var font in _fonts)
            {
                fontCount++;
                objectCount++;
                font.Id = string.Format("F{0}", fontCount); 
                font.ObjectNumber = objectCount;
            }

            foreach (var font in FontFiles)
            {
                objectCount++;
                font.Id = _fonts.Where(f => f.FontName == font.Name).First().Id;
                font.ObjectNumber = objectCount;
                objectCount++; // take the actual font file into account as well
            }

            foreach (var image in Images)
            {
                imageCount++;
                objectCount++;
                image.Id = string.Format("I{0}", imageCount);
                image.ObjectNumber = objectCount;

                if (image.MaskData != null)
                {
                    // take any mask data into account as well
                    imageCount++;
                    objectCount++;
                    image.MaskData.Id = string.Format("I{0}", imageCount);
                    image.MaskData.ObjectNumber = objectCount;
                }
            }

            // Allow for the parent page
            objectCount++;
            _pageRoot.Id = "R1";
            _pageRoot.ObjectNumber = objectCount;

            // The header and footer content
            foreach (var headerfooter in HeadersFooters)
            {
                foreach (var content in headerfooter.Contents)
                {
                    contentCount++;
                    objectCount++;
                    content.Id = string.Format("C{0}", contentCount);
                    content.ObjectNumber = objectCount;
                }
            }

            // Do the page content
            foreach (var page in Pages)
            {
                // Attach the page root to each page
                page.PageRoot = _pageRoot;

                foreach (var content in page.Contents)
                {
                    contentCount++;
                    objectCount++;
                    content.Id = string.Format("C{0}", contentCount);
                    content.ObjectNumber = objectCount;
                }
            }

            // Do the pages
            foreach (var page in Pages)
            {
                pageCount++;
                objectCount++;
                page.Id = string.Format("P{0}", pageCount);
                page.ObjectNumber = objectCount;
            }

            // Document Info properties
            objectCount++;
            Properties.Id = "N1";
            Properties.ObjectNumber = objectCount;

            return objectCount;
        }

    }

}
