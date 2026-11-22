using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace CorePDF.Embeds
{
    public enum FontType
    {
        Type0,
        Type1,
        TrueType,
        CIDFontType2
    }

    /// <summary>
    /// A TrueType font file to be embedded in the document. Only the name and the file (or its
    /// contents) are needed: the font's metrics are read from the file itself, and only the
    /// characters actually used are included in the published document.
    /// </summary>
    public class FontFile
    {
        private const string METRICSOBSOLETE = "Font metrics are now read from the font file automatically, so this value is ignored.";

        /// <summary>
        /// The name used to reference this font in the document content
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// The family name of the font. Fonts with the same BaseFont are treated as the bold
        /// and italic variants of each other when using &lt;b&gt; and &lt;i&gt; tags in text.
        /// Defaults to the Name if left blank.
        /// </summary>
        public string BaseFont { get; set; }

        /// <summary>
        /// True if this is the bold variant of the font family
        /// </summary>
        public bool Italic { get; set; }

        /// <summary>
        /// True if this is the italic variant of the font family
        /// </summary>
        public bool Bold { get; set; }

        /// <summary>
        /// A fully qualified path to the font file
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// The raw contents of the font file. Used instead of FilePath if set.
        /// </summary>
        public byte[] FileData { get; set; }

        [Obsolete(METRICSOBSOLETE), JsonIgnore]
        public int BoundingBoxLeft { get; set; }
        [Obsolete(METRICSOBSOLETE), JsonIgnore]
        public int Flags { get; set; }
        [Obsolete(METRICSOBSOLETE), JsonIgnore]
        public int Ascent { get; set; }
        [Obsolete(METRICSOBSOLETE), JsonIgnore]
        public int CapHeight { get; set; }
        [Obsolete(METRICSOBSOLETE), JsonIgnore]
        public int Descent { get; set; }
        [Obsolete(METRICSOBSOLETE), JsonIgnore]
        public int AverageWidth { get; set; }
        [Obsolete(METRICSOBSOLETE), JsonIgnore]
        public int MaximumWidth { get; set; }
        [Obsolete(METRICSOBSOLETE), JsonIgnore]
        public int ItalicAngle { get; set; }
        [Obsolete(METRICSOBSOLETE), JsonIgnore]
        public int FontWeight { get; set; }
        [Obsolete(METRICSOBSOLETE), JsonIgnore]
        public int XHeight { get; set; }
        [Obsolete(METRICSOBSOLETE), JsonIgnore]
        public int StemV { get; set; }
        [Obsolete(METRICSOBSOLETE), JsonIgnore]
        public List<int> Widths { get; set; }
        [Obsolete("Embedded fonts are always written as Type0 fonts, so this value is ignored."), JsonIgnore]
        public FontType Type { get; set; } = FontType.TrueType;

        /// <summary>
        /// Reads the contents of the font file
        /// </summary>
        internal byte[] ReadData()
        {
            if (FileData != null)
            {
                return FileData;
            }

            if (string.IsNullOrEmpty(FilePath))
            {
                throw new InvalidOperationException($"The font '{Name}' has neither a FilePath nor FileData.");
            }

            if (!File.Exists(FilePath))
            {
                throw new FileNotFoundException($"The font file for '{Name}' could not be found.", FilePath);
            }

            return File.ReadAllBytes(FilePath);
        }
    }

}
