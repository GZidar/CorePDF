using CorePDF.Contents;
using CorePDF.TypeFaces;
using System.Collections.Generic;
using System.IO;

namespace CorePDF.Pages
{
    /// <summary>
    /// Defines any repeating content that is to be included at either the top 
    /// or bottom of the document pages.
    /// </summary>
    public class HeaderFooter : PDFObject
    {
        private Size _pageSize { get; set; } = Paper.Size("a4P");

        /// <summary>
        /// The name of the Header or footer. Used by the pages to reference this definition
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The various content elements that form the header or footer. 
        /// </summary>
        public List<Content> Contents { get; set; } = new List<Content>();

        /// <summary>
        /// The page size and orientation that this header or footer applies to.
        /// </summary>
        public string PageSize { get; set; } = Paper.PAGEA4PORTRAIT;

        public void PrepareStream(PageRoot pageRoot, List<Font> fonts, bool compress = false)
        {
            // Get the details of the paper that matches the requested size
            _pageSize = Paper.Size(PageSize);

            foreach (var content in Contents)
            {
                content.PrepareStream(pageRoot, _pageSize, fonts, compress);
            }
        }

        public override void Publish(StreamWriter stream)
        {
            foreach (var content in Contents)
            {
                content.Publish(stream);
            }
        }
    }
}
