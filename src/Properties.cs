using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CorePDF
{
    public class Properties : PDFObject
    {
        private string Creator { get; set; } = "CorePDF";
        public string Title { get; set; }
        public string Subject { get; set; }
        public string Author { get; set; }
        public string Keywords { get; set; }
        public DateTime? CreationDate { get; set; }
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
            var result = string.Format("(D:{0:yyyyMMddhhmmss}", input);

            var utcDate = DateTime.UtcNow;
            var diff = input.Subtract(utcDate);
            var utcHour = diff.Hours;
            var utcMinute = diff.Minutes;
            char sign = '+';
            if (utcHour < 0)
            {
                sign = '-';
            }
            utcHour = Math.Abs(utcHour);

            result += string.Format("{0}{1}'{2}')", sign, utcHour.ToString().PadLeft(2, '0'), utcMinute.ToString().PadLeft(2, '0'));

            return result;
        }
    }
}
