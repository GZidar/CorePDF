using CorePDF.Embeds;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CorePDF.TypeFaces
{
    /// <summary>
    /// Holds the detail of any fonts used in the document 
    /// </summary>
    public class Font : PDFObject
    {
        /// <summary>
        /// The specific name of the font
        /// </summary>
        public string FontName { get; set; }

        /// <summary>
        /// The base name of the font. This is used to relate the
        /// various versions of the same font.
        /// </summary>
        public string BaseFont { get; set; }

        /// <summary>
        /// Maybe Type1, TrueType or one of several other values
        /// </summary>
        public string Type { get; set; } = "Type1";

        /// <summary>
        /// True if this is a bold font
        /// </summary>
        public bool Bold { get; set; }

        /// <summary>
        /// True if this is an italic font
        /// </summary>
        public bool Italic { get; set; }

        /// <summary>
        /// The character encoding of the font
        /// </summary>
        public string Encoding { get; set; } = "WinAnsiEncoding";

        /// <summary>
        /// Holds the size of each character in the printable character set
        /// </summary>
        public List<int> Metrics { get; set; }

        public void Publish(StreamWriter stream, List<FontFile> fontFiles)
        {
            var PDFData = new Dictionary<string, dynamic>
            {
                { "/Type", "/Font" },
                { "/Subtype", "/" + Type},
                { "/Name", "/" + Id},
                { "/BaseFont", "/" + FontName},
                { "/Encoding", "/" + Encoding}
            };

            if (Type != "Type1")
            {
                var fontFile = fontFiles.Where(f => f.Name == FontName).First();

                var widths = "";
                foreach (var number in Metrics)
                {
                    widths += number.ToString() + ", ";
                }
                if (! string.IsNullOrEmpty(widths))
                {
                    // get rid of the last comma and space
                    widths = widths.TrimEnd(',',' ');
                }

                PDFData.Add("/FontDescriptor", string.Format("{0} 0 R", fontFile.ObjectNumber));
                PDFData.Add("/FirstChar", 0);
                PDFData.Add("/LastChar", 255);
                if (!string.IsNullOrEmpty(widths))
                {
                    PDFData.Add("/Widths", string.Format("[ {0} ]", widths));
                }
            }

            _pdfObject = PDFData;

            base.Publish(stream);
        }
    }
}