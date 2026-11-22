using CorePDF.Contents;
using CorePDF.TypeFaces;
using System.Collections.Generic;
using System.IO;

namespace CorePDF.Pages
{
    /// <summary>
    /// Defines the content of each page in the document
    /// </summary>
    public class Page : PDFObject
    {
        private Size _pageSize { get; set; }

        /// <summary>
        /// The header (if any) that this page uses (taken from the set defined in the document)
        /// </summary>
        public string HeaderName { get; set; }

        /// <summary>
        /// The footer (if any) that this page uses (taken from the set defined in the document)
        /// </summary>
        public string FooterName { get; set; }

        /// <summary>
        /// The paper size to use for this page
        /// </summary>
        public string PageSize { get; set; } = Paper.PAGEA4PORTRAIT;

        /// <summary>
        /// The document page root. This value is set from the document object
        /// </summary>
        public PageRoot PageRoot { get; set; }

        /// <summary>
        /// Any content that is to be shown on the page
        /// </summary>
        public List<Content> Contents { get; set; } = new List<Content>();

        public void PrepareStream(List<Font> documentFonts, bool compress = false)
        {
            // Get the details of the paper that matches the requested size
            _pageSize = Paper.Size(PageSize);

            foreach (var content in Contents)
            {
                content.PrepareStream(PageRoot, _pageSize, documentFonts, compress);
            }
        }

        /// <summary>
        /// The clickable links on the page (including any in its header or footer). This is
        /// set by the document once the page content has been prepared.
        /// </summary>
        internal List<LinkArea> Links { get; set; } = new List<LinkArea>();

        /// <summary>
        /// The object number of the first link annotation; the rest follow on consecutively.
        /// </summary>
        internal int FirstAnnotationNumber { get; set; }

        /// <summary>
        /// Gathers the links from the page's header, footer and content
        /// </summary>
        internal void CollectLinks()
        {
            Links = new List<LinkArea>();
            foreach (var content in AllContents())
            {
                Links.AddRange(content.GetLinks());
            }
        }

        /// <summary>
        /// The content of the page's header and footer followed by the page's own content,
        /// in the order it is drawn
        /// </summary>
        private IEnumerable<Content> AllContents()
        {
            var header = PageRoot.Document.GetHeaderFooter(HeaderName);
            if (header != null)
            {
                foreach (var content in header.Contents) yield return content;
            }

            var footer = PageRoot.Document.GetHeaderFooter(FooterName);
            if (footer != null)
            {
                foreach (var content in footer.Contents) yield return content;
            }

            foreach (var content in Contents) yield return content;
        }

        public void Publish(List<Font> documentFonts, StreamWriter stream)
        {
            // only the fonts that are used are included in the document
            var fonts = new Dictionary<string, string>();
            foreach (var font in documentFonts)
            {
                if (font.Used)
                {
                    fonts["/" + font.Id] = string.Format("{0} 0 R", font.ObjectNumber);
                }
            }

            // content can be found on the page and in any associated header or footer
            var contentRefs = "[ ";
            var xobjects = new Dictionary<string, string>();

            foreach (var content in AllContents())
            {
                if (!content.HasStream) continue; // nothing to draw (eg: an empty text box)

                contentRefs += string.Format("{0} 0 R ", content.ObjectNumber);

                if (content is Image)
                {
                    var imageFile = PageRoot.Document.GetImage(((Image)content).ImageName);
                    if (imageFile != null && imageFile.IsPublished)
                    {
                        xobjects["/" + imageFile.Id] = string.Format("{0} 0 R", imageFile.ObjectNumber);
                    }
                }
            }
            contentRefs += "]";

            var resources = new Dictionary<string, dynamic>();
            resources.Add("/ProcSet", "[/PDF /Text /ImageB /ImageC /ImageI]");
            if (fonts.Count > 0)
            {
                resources.Add("/Font", fonts);
            }
            if (xobjects.Count > 0)
            {
                resources.Add("/XObject", xobjects);
            }

            _pdfObject = new Dictionary<string, dynamic>
            {
                { "/Type", "/Page" },
                { "/Parent", string.Format("{0} 0 R", PageRoot.ObjectNumber)},
                { "/MediaBox", string.Format("[0 0 {0} {1}]", _pageSize.ContentWidth, _pageSize.ContentHeight)},
                { "/Resources", resources },
                { "/Contents", contentRefs}
            };

            if (Links.Count > 0)
            {
                var annots = "[";
                for (var i = 0; i < Links.Count; i++)
                {
                    annots += string.Format(" {0} 0 R", FirstAnnotationNumber + i);
                }
                _pdfObject.Add("/Annots", annots + " ]");
            }

            base.Publish(stream);
        }

    }

}
