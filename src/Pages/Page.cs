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
        public HeaderFooter Header { get; set; }

        /// <summary>
        /// The footer (if any) that this page uses (taken from the set defined in the document)
        /// </summary>
        public HeaderFooter Footer { get; set; }

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
                content.PrepareStream(_pageSize, documentFonts, compress);
            }
        }

        public void Publish(List<Font> documentFonts, StreamWriter stream)
        {
            var fonts = new Dictionary<string, string>();
            foreach (var font in documentFonts)
            {
                fonts.Add("/" + font.Id, string.Format("{0} 0 R", font.ObjectNumber));
            }

            // content can be found on the page and in any associated header or footer
            var contentRefs = "[ ";
            var xobjects = new Dictionary<string, string>();
            if (Header != null)
            {
                foreach (var content in Header.Contents)
                {
                    contentRefs += string.Format("{0} 0 R ", content.ObjectNumber);

                    if (content is Image)
                    {
                        xobjects.Add("/" + ((Image)content).ImageFile.Id, string.Format("{0} 0 R", ((Image)content).ImageFile.ObjectNumber));
                    }
                }
            }
            if (Footer != null)
            {
                foreach (var content in Footer.Contents)
                {
                    contentRefs += string.Format("{0} 0 R ", content.ObjectNumber);

                    if (content is Image)
                    {
                        xobjects.Add("/" + ((Image)content).ImageFile.Id, string.Format("{0} 0 R", ((Image)content).ImageFile.ObjectNumber));
                    }
                }
            }
            foreach (var content in Contents)
            {
                contentRefs += string.Format("{0} 0 R ", content.ObjectNumber);

                if (content is Image)
                {
                    xobjects.Add("/" + ((Image)content).ImageFile.Id, string.Format("{0} 0 R", ((Image)content).ImageFile.ObjectNumber));
                }
            }
            contentRefs += "]";

            var resources = new Dictionary<string, dynamic>();
            resources.Add("/ProcSet", "[/PDF /Text /ImageB]");
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

            base.Publish(stream);
        }

    }

}
