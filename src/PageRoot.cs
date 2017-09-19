using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CorePDF
{
    /// <summary>
    /// The page root object of the document
    /// </summary>
    public class PageRoot : PDFObject
    {
        /// <summary>
        /// A reference back up to the document
        /// </summary>
        public virtual Document Document { get; set; }

        public override void Publish(StreamWriter stream)
        {
            var pageRefs = "[";
            foreach (var page in Document.Pages)
            {
                pageRefs += string.Format(" {0} 0 R", page.ObjectNumber);
            }
            pageRefs += " ]";

            var PDFData = new Dictionary<string, dynamic>
            {
                { "/Type", "/Pages" },
                { "/Count", Document.Pages.Count.ToString() },
                { "/Kids",  pageRefs}
            };

            _pdfObject = PDFData;

            base.Publish(stream);
        }

    }
}
