using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CorePDF.TypeFaces
{
   public class Font : PDFObject
    {
        public string FontName { get; set; }
        public string BaseFont { get; set; }
        public string Type { get; set; }
        public bool Bold { get; set; }
        public bool Italic { get; set; }
        public string Encoding { get; set; }
        public List<int> Metrics { get; set; }

        public override void Publish(StreamWriter stream)
        {
            var PDFData = new Dictionary<string, dynamic>
            {
                { "/Type", "/Font" },
                { "/Subtype", "/" + Type},
                { "/Name", "/" + Id},
                { "/BaseFont", "/" + FontName},
                { "/Encoding", "/" + Encoding}
            };

            _pdfObject = PDFData;

            base.Publish(stream);
        }
    }
}