using CorePDF.Writing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace CorePDF.TypeFaces
{
    /// <summary>
    /// Holds the detail of any fonts used in the document. This class represents the standard
    /// fonts that every PDF reader has available (Helvetica, Times, Courier, etc) which use
    /// the single byte WinAnsiEncoding. Fonts embedded from a font file are handled by
    /// <see cref="EmbeddedFont"/>.
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
        /// The character encoding of the font. Symbolic fonts (Symbol and ZapfDingbats) use
        /// their own built-in encoding and should leave this empty.
        /// </summary>
        public string Encoding { get; set; } = "WinAnsiEncoding";

        /// <summary>
        /// Holds the width of each character, indexed by its code in the font's encoding
        /// </summary>
        public List<int> Metrics { get; set; }

        /// <summary>
        /// Set when text has been written using this font. Only fonts that are used are
        /// included in the document.
        /// </summary>
        internal bool Used { get; set; }

        /// <summary>
        /// True if the font program is included in the document
        /// </summary>
        internal virtual bool IsEmbedded => false;

        /// <summary>
        /// Calculates the width of the text in thousandths of the font size
        /// </summary>
        public virtual int MeasureText(string text)
        {
            var width = 0;
            foreach (var code in EncodeBytes(text))
            {
                width += code < Metrics.Count ? Metrics[code] : 0;
            }
            return width;
        }

        /// <summary>
        /// Converts the text into a PDF string, ready to be shown with the Tj operator
        /// </summary>
        public virtual string EncodeText(string text)
        {
            Used = true;
            return PdfString.Literal(EncodeBytes(text));
        }

        /// <summary>
        /// Called before the document is written so the font can reserve object numbers for
        /// itself and any objects it depends on. Returns the next free object number.
        /// </summary>
        internal virtual int AssignObjectNumbers(int nextObjectNumber)
        {
            ObjectNumber = nextObjectNumber;
            return nextObjectNumber + 1;
        }

        public override void Publish(StreamWriter stream)
        {
            _pdfObject = FontDictionary();

            base.Publish(stream);
        }

        /// <summary>
        /// The font dictionary that content streams refer to
        /// </summary>
        protected virtual Dictionary<string, dynamic> FontDictionary()
        {
            var PDFData = new Dictionary<string, dynamic>
            {
                { "/Type", "/Font" },
                { "/Subtype", "/" + Type},
                { "/Name", "/" + Id},
                { "/BaseFont", "/" + FontName}
            };

            if (!string.IsNullOrEmpty(Encoding))
            {
                PDFData.Add("/Encoding", "/" + Encoding);
            }

            return PDFData;
        }

        private List<byte> EncodeBytes(string text)
        {
            var result = new List<byte>();
            foreach (var rune in (text ?? "").EnumerateRunes())
            {
                if (FontText.IsNeverShown(rune.Value)) continue;

                byte code;
                if (string.IsNullOrEmpty(Encoding))
                {
                    // symbolic fonts use their built-in encoding, so the character code is used as is
                    if (rune.Value > 0xFF)
                    {
                        throw UnsupportedCharacter(rune);
                    }
                    code = (byte)rune.Value;
                }
                else if (!WinAnsiEncoding.TryEncode(rune.Value, out code))
                {
                    if (FontText.IsIgnorable(rune.Value)) continue;
                    throw UnsupportedCharacter(rune);
                }

                result.Add(code);
            }
            return result;
        }

        private ArgumentException UnsupportedCharacter(Rune rune)
        {
            return new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                "The character '{0}' (U+{1:X4}) cannot be shown using the standard font '{2}'. " +
                "Embed a TrueType font that contains this character (see Document.EmbedFont) and use that instead.",
                rune.ToString(), rune.Value, FontName));
        }
    }

    internal static class FontText
    {
        /// <summary>
        /// Characters that are never drawn: soft hyphens only appear when a word is hyphenated
        /// at the end of a line (which isn't done), and control characters have no glyph.
        /// </summary>
        public static bool IsNeverShown(int codePoint)
        {
            return codePoint == 0x00AD || codePoint < 0x20;
        }

        /// <summary>
        /// Characters that have no visible form, so can be dropped if a font has no glyph for them
        /// </summary>
        public static bool IsIgnorable(int codePoint)
        {
            return codePoint == 0x00AD // soft hyphen
                || (codePoint >= 0x200B && codePoint <= 0x200F) // zero width spaces, joiners and direction marks
                || codePoint == 0x2060 // word joiner
                || codePoint == 0xFEFF // byte order mark
                || (codePoint >= 0xFE00 && codePoint <= 0xFE0F) // variation selectors
                || codePoint < 0x20; // control characters
        }
    }
}
