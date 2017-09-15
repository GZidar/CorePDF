using CorePDF.Contents;
using CorePDF.TypeFaces;
using System.Collections.Generic;
using System.IO;

namespace CorePDF.Pages
{
    public class HeaderFooter : PDFObject
    {
        public List<Content> Contents { get; set; } = new List<Content>();
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
