using CorePDF.Conformance;
using CorePDF.Writing;
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
        /// The object number of the XMP metadata stream (zero if there isn't one)
        /// </summary>
        internal int MetadataNumber { get; set; }

        /// <summary>
        /// The object number of the output intent's ICC profile (zero if there isn't one)
        /// </summary>
        internal int OutputProfileNumber { get; set; }

        public void Publish(PageRoot pageRoot, StreamWriter stream)
        {
            var PDFData = new Dictionary<string, dynamic>
            {
                { "/Type", "/Catalog" },
                { "/Pages", string.Format("{0} 0 R", pageRoot.ObjectNumber) }
            };

            if (MetadataNumber > 0)
            {
                PDFData.Add("/Metadata", string.Format("{0} 0 R", MetadataNumber));
            }

            if (OutputProfileNumber > 0)
            {
                // describes the colour space the document's RGB colours are in
                var name = PdfString.Text(SrgbIccProfile.Description);
                PDFData.Add("/OutputIntents", string.Format(
                    "[<< /Type /OutputIntent /S /GTS_PDFA1 /OutputConditionIdentifier {0} /Info {0} /DestOutputProfile {1} 0 R >>]",
                    name, OutputProfileNumber));
            }

            _pdfObject = PDFData;

            base.Publish(stream);
        }
    }
}
