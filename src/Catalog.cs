using System.Collections.Generic;
using System.IO;

namespace CorePDF
{
    /// <summary>
    /// The catalog object of the PDF Document
    /// </summary>
    public class Catalog : PDFObject
    {
        /// <summary>
        /// A reference back to the document
        /// </summary>
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
