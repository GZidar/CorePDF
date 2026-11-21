using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CorePDF
{
    /// <summary>
    /// Holds details of any document properties
    /// </summary>
    public class Properties : PDFObject
    {
        private string Creator { get; set; } = "CorePDF";

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
                result["/Title"] = string.Format("({0})", Title);
            }

            if (!string.IsNullOrEmpty(Subject))
            {
                result["/Subject"] = string.Format("({0})", Subject);
            }

            if (!string.IsNullOrEmpty(Author))
            {
                result["/Author"] = string.Format("({0})", Author);
            }

            if (!string.IsNullOrEmpty(Keywords))
            {
                result["/Keywords"] = string.Format("({0})", Keywords);
            }

            if (!string.IsNullOrEmpty(Creator))
            {
                result["/Creator"] = string.Format("({0})", Creator);
            }

            if (CreationDate != null)
            {
                result["/CreationDate"] = toPDFDate(CreationDate.Value);
            }

            if (ModifiedDate != null)
            {
                result["/ModDate"] = toPDFDate(ModifiedDate.Value);
            }

            _pdfObject = result;

            base.Publish(stream);
        }

        private string toPDFDate(DateTime input)
        {
            // UTC dates use offset zero; local and unspecified dates use the local time zone's
            // offset at that date (so daylight saving is taken into account)
            var offset = new DateTimeOffset(input).Offset;
            var sign = offset < TimeSpan.Zero ? '-' : '+';
            offset = offset.Duration();

            return string.Format(CultureInfo.InvariantCulture, "(D:{0:yyyyMMddHHmmss}{1}{2:00}'{3:00}')",
                input, sign, offset.Hours, offset.Minutes);
        }
    }
}
