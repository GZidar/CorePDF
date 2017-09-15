using CorePDF.Contents;
using CorePDF.TypeFaces;
using System.Collections.Generic;
using System.IO;

namespace CorePDF.Pages
{

    public class Page : PDFObject
    {
        public HeaderFooter Header { get; set; }
        public HeaderFooter Footer { get; set; }
        public Size PageSize { get; set; }
        public PageRoot PageRoot { get; set; }
        public List<Content> Contents { get; set; } = new List<Content>();

        public void PrepareStream(List<Font> documentFonts, bool compress = false)
        {
            foreach (var content in Contents)
            {
                content.PrepareStream(PageSize, documentFonts, compress);
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
                { "/MediaBox", string.Format("[0 0 {0} {1}]", PageSize.ContentWidth, PageSize.ContentHeight)},
                { "/Resources", resources },
                { "/Contents", contentRefs}
            };

            base.Publish(stream);
        }

    }

}
