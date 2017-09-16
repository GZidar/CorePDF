using System.Collections.Generic;
using System.IO;

namespace CorePDF
{
    /// <summary>
    /// The catalog object of the PDF Document
    /// </summary>
    public class Catalog : PDFObject
    {
        public void Publish(PageRoot pageRoot, StreamWriter stream)
        {
            var PDFData = new Dictionary<string, dynamic>
            {
                { "/Type", "/Catalog" },
                { "/Pages", string.Format("{0} 0 R", pageRoot.ObjectNumber) }
            };

            _pdfObject = PDFData;

            base.Publish(stream);
        }
    }
}
