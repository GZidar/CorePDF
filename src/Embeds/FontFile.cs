using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CorePDF.Embeds
{
    public enum FontType
    {
        Type0,
        Type1,
        TrueType,
        CIDFontType2
    }

    public class FontFile : PDFObject
    {
        /// <summary>
        /// The name used to reference this font in the document content
        /// </summary>
        public string Name { get; set; }

        public string BaseFont { get; set; }
        public bool Italic { get; set; }
        public bool Bold { get; set; }

        public FontType Type { get; set; } = FontType.TrueType;

        /// <summary>
        /// A fully qualified path to the font file
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// The raw contents of the font file
        /// </summary>
        public byte[] FileData { get; set; }

        /// <summary>
        /// The size of each character relative to one another
        /// </summary>
        public List<int> Widths { get; set; }

        protected Stream Open()
        {
            if (FileData == null)
            {
                return new FileStream(FilePath, FileMode.Open, FileAccess.Read);
            }

            return new MemoryStream(FileData);
        }

        public void EmbedFile()
        {
            // do nothing if there is no valid file or file data specified
            if (FileData == null && (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))) return;

            // attempt to load the font file to get the metrics information from it
            using (var fileStream = Open())
            {
                var memStream = new MemoryStream();
                fileStream.CopyTo(memStream);

                _encodedData = memStream.ToArray();
            }
        }

        public override void Publish(StreamWriter stream)
        {
            var PDFData = new Dictionary<string, dynamic>
            {
                { "/Type", "/FontDescriptor" },
                { "/FontName", "/" + Name },
                { "/ItalicAngle", 0},
                { "/FontWeight", 400},
                { "/MaxWidth", Widths.Max() },
                { "/AvgWidth", Widths.Where(a => a > 0).Average() },
                { "/FontFile2", string.Format("{0} 0 R", ObjectNumber)}
            };

            // save the embedded font data
            //var PDFData = new Dictionary<string, dynamic>
            //{
            //    { "/Type", "/Font" },
            //    { "/Subtype", "/" + Type.ToString() },
            //    { "/Name", "/" + Id },
            //    { "/BaseFont", "/" + Name },
            //    { "/Encoding", "/WinAnsiEncoding" },
            //    { "/FontDescriptor", descriptor },
            //    { "/FirstChar", "0" },
            //    { "/LastChar", "255" },
            //    { "/Widths", Widths },
            //};

            _pdfObject = PDFData;
        }
    }

}
