using System.Collections.Generic;
using System.IO;

namespace CorePDF
{
    public class Catalog : PDFObject
    {
        public Document Document { get; set; }

        public override void Publish(StreamWriter stream)
        {
            var PDFData = new Dictionary<string, dynamic>
            {
                { "/Type", "/Catalog" },
                { "/Pages", string.Format("{0} 0 R", Document.PageRoot.ObjectNumber) }
            };

            _pdfObject = PDFData;

            base.Publish(stream);
        }
    }
}
