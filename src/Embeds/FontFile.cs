using Newtonsoft.Json;
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
        public int Flags { get; set; }
        public int CapHeight { get; set; }
        public int Descent { get; set; }
        public int AverageWidth { get; set; }
        public int MaximumWidth { get; set; }
        public int ItalicAngle { get; set; } = 0;
        public int FontWeight { get; set; } = 400;
        public int StemV { get; set; }

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
        /// This will be calculated as the document is published
        /// </summary>
        [JsonIgnore]
        public long FileBytePosition { get; set; }

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
                { "/Flags", Flags},
                { "/ItalicAngle", ItalicAngle},
                { "/Ascent", CapHeight + 1},
                { "/Descent", Descent },
                { "/CapHeight", CapHeight },
                { "/FontWeight", FontWeight },
                { "/MaxWidth", MaximumWidth },
                { "/AvgWidth", AverageWidth },
                { "/StemV", StemV},
                { "/FontBBox", string.Format("[ {0} {1} {2} {3} ]", Descent - 2, Descent, MaximumWidth + (Descent - 2), CapHeight)},
                { "/FontFile2", string.Format("{0} 0 R", ObjectNumber + 1)}
            };

            _pdfObject = PDFData;

            var encodedData = _encodedData;
            var compressed = _compressed;

            // clear out the data and compressed values for the Font Descriptor
            _compressed = false;
            _encodedData = null;
            base.Publish(stream);

            var bytePosition = BytePosition;

            // restore those values to publish the actual font data
            _encodedData = encodedData;
            _compressed = compressed;
            _pdfObject = new Dictionary<string, dynamic>();
            ObjectNumber += 1;
            base.Publish(stream);

            // Save the byte position of the file and restore the previous values
            // for the font descriptor
            FileBytePosition = BytePosition;
            BytePosition = bytePosition;
            ObjectNumber -= 1;
        }
    }

}
