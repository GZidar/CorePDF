using CorePDF.Writing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Security;
using System.Text;

namespace CorePDF
{
    /// <summary>
    /// Holds details of any document properties
    /// </summary>
    public class Properties : PDFObject
    {
        private const string CREATOR = "CorePDF";
        private const string PRODUCER = "CorePDF";

        /// <summary>
        /// The document title
        /// </summary>
        public string Title { get; set; }

        /// <summary>
        /// The subject of the document
        /// </summary>
        public string Subject { get; set; }

        /// <summary>
        /// The author of the document
        /// </summary>
        public string Author { get; set; }

        /// <summary>
        /// Any keywords associated with the document
        /// </summary>
        public string Keywords { get; set; }

        /// <summary>
        /// The date the document is created
        /// </summary>
        public DateTime? CreationDate { get; set; }

        /// <summary>
        /// The date that the document was last modified
        /// </summary>
        public DateTime? ModifiedDate { get; set; }

        public override void Publish(StreamWriter stream)
        {
            var result = new Dictionary<string, dynamic>();

            if (!string.IsNullOrEmpty(Title))
            {
                result["/Title"] = PdfString.Text(Title);
            }

            if (!string.IsNullOrEmpty(Subject))
            {
                result["/Subject"] = PdfString.Text(Subject);
            }

            if (!string.IsNullOrEmpty(Author))
            {
                result["/Author"] = PdfString.Text(Author);
            }

            if (!string.IsNullOrEmpty(Keywords))
            {
                result["/Keywords"] = PdfString.Text(Keywords);
            }

            result["/Creator"] = PdfString.Text(CREATOR);
            result["/Producer"] = PdfString.Text(PRODUCER);

            if (CreationDate != null)
            {
                result["/CreationDate"] = ToPDFDate(CreationDate.Value);
            }

            if (ModifiedDate != null)
            {
                result["/ModDate"] = ToPDFDate(ModifiedDate.Value);
            }

            _pdfObject = result;

            base.Publish(stream);
        }

        /// <summary>
        /// Creates the XMP metadata packet for the document. The values must match those in the
        /// document information dictionary written by Publish.
        /// </summary>
        internal string ToXmp(PdfConformance conformance)
        {
            var xmp = new StringBuilder();
            xmp.Append("<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n");
            xmp.Append("<x:xmpmeta xmlns:x=\"adobe:ns:meta/\">\n");
            xmp.Append(" <rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">\n");

            if (conformance != PdfConformance.None)
            {
                xmp.Append("  <rdf:Description rdf:about=\"\" xmlns:pdfaid=\"http://www.aiim.org/pdfa/ns/id/\">\n");
                xmp.Append("   <pdfaid:part>2</pdfaid:part>\n");
                xmp.AppendFormat("   <pdfaid:conformance>{0}</pdfaid:conformance>\n", conformance == PdfConformance.PdfA2U ? "U" : "B");
                xmp.Append("  </rdf:Description>\n");
            }

            xmp.Append("  <rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">\n");
            xmp.Append("   <dc:format>application/pdf</dc:format>\n");
            if (!string.IsNullOrEmpty(Title))
            {
                xmp.AppendFormat("   <dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">{0}</rdf:li></rdf:Alt></dc:title>\n", Escape(Title));
            }
            if (!string.IsNullOrEmpty(Author))
            {
                xmp.AppendFormat("   <dc:creator><rdf:Seq><rdf:li>{0}</rdf:li></rdf:Seq></dc:creator>\n", Escape(Author));
            }
            if (!string.IsNullOrEmpty(Subject))
            {
                xmp.AppendFormat("   <dc:description><rdf:Alt><rdf:li xml:lang=\"x-default\">{0}</rdf:li></rdf:Alt></dc:description>\n", Escape(Subject));
            }
            xmp.Append("  </rdf:Description>\n");

            xmp.Append("  <rdf:Description rdf:about=\"\" xmlns:xmp=\"http://ns.adobe.com/xap/1.0/\">\n");
            xmp.AppendFormat("   <xmp:CreatorTool>{0}</xmp:CreatorTool>\n", CREATOR);
            if (CreationDate != null)
            {
                xmp.AppendFormat("   <xmp:CreateDate>{0}</xmp:CreateDate>\n", ToXmpDate(CreationDate.Value));
            }
            if (ModifiedDate != null)
            {
                xmp.AppendFormat("   <xmp:ModifyDate>{0}</xmp:ModifyDate>\n", ToXmpDate(ModifiedDate.Value));
            }
            xmp.Append("  </rdf:Description>\n");

            xmp.Append("  <rdf:Description rdf:about=\"\" xmlns:pdf=\"http://ns.adobe.com/pdf/1.3/\">\n");
            xmp.AppendFormat("   <pdf:Producer>{0}</pdf:Producer>\n", PRODUCER);
            if (!string.IsNullOrEmpty(Keywords))
            {
                xmp.AppendFormat("   <pdf:Keywords>{0}</pdf:Keywords>\n", Escape(Keywords));
            }
            xmp.Append("  </rdf:Description>\n");

            xmp.Append(" </rdf:RDF>\n");
            xmp.Append("</x:xmpmeta>\n");
            xmp.Append("<?xpacket end=\"w\"?>");

            return xmp.ToString();
        }

        private static string Escape(string value)
        {
            return SecurityElement.Escape(value);
        }

        private static TimeSpan GetOffset(DateTime input)
        {
            // UTC dates use offset zero; local and unspecified dates use the local time zone's
            // offset at that date (so daylight saving is taken into account)
            return new DateTimeOffset(input).Offset;
        }

        private static string ToPDFDate(DateTime input)
        {
            var offset = GetOffset(input);
            var sign = offset < TimeSpan.Zero ? '-' : '+';
            offset = offset.Duration();

            return string.Format(CultureInfo.InvariantCulture, "(D:{0:yyyyMMddHHmmss}{1}{2:00}'{3:00}')",
                input, sign, offset.Hours, offset.Minutes);
        }

        private static string ToXmpDate(DateTime input)
        {
            var offset = GetOffset(input);
            var sign = offset < TimeSpan.Zero ? '-' : '+';
            offset = offset.Duration();

            return string.Format(CultureInfo.InvariantCulture, "{0:yyyy-MM-ddTHH:mm:ss}{1}{2:00}:{3:00}",
                input, sign, offset.Hours, offset.Minutes);
        }
    }
}
