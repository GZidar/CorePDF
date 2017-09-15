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
        /// <summary>
        /// The various content elements that form the header or footer. 
        /// </summary>
        public List<Content> Contents { get; set; } = new List<Content>();

        /// <summary>
        /// The page size and orientation that this header or footer applies to.
        /// </summary>
        public Size PageSize { get; set; } = Paper.Size("a4P");

        public void PrepareStream(List<Font> fonts, bool compress = false)
        {
            foreach (var content in Contents)
            {
                content.PrepareStream(PageSize, fonts, compress);
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
