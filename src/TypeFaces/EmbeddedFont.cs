using CorePDF.Embeds;
using CorePDF.Writing;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;

namespace CorePDF.TypeFaces
{
    /// <summary>
    /// A TrueType font embedded in the document. These are written as composite (Type0) fonts
    /// using two byte glyph ids, which allows any character the font contains to be used.
    /// A ToUnicode map is included so the text can be searched, selected and copied, and the
    /// font program is subset to only include the glyphs that are used.
    /// </summary>
    public sealed class EmbeddedFont : Font
    {
        private const int OBJECTCOUNT = 5;

        private readonly TrueTypeFont _ttf;
        private readonly SortedDictionary<int, string> _usedGlyphs = new SortedDictionary<int, string>();

        internal EmbeddedFont(FontFile fontFile, TrueTypeFont ttf)
        {
            _ttf = ttf;

            FontName = fontFile.Name;
            BaseFont = string.IsNullOrEmpty(fontFile.BaseFont) ? fontFile.Name : fontFile.BaseFont;
            Bold = fontFile.Bold;
            Italic = fontFile.Italic;
            Type = "Type0";
            Encoding = "Identity-H";
        }

        internal override bool IsEmbedded => true;

        /// <summary>
        /// Calculates the width of the text in thousandths of the font size
        /// </summary>
        public override int MeasureText(string text)
        {
            var width = 0;
            foreach (var rune in (text ?? "").EnumerateRunes())
            {
                var gid = GetGlyph(rune);
                if (gid > 0)
                {
                    width += GlyphWidth(gid);
                }
            }
            return width;
        }

        /// <summary>
        /// Converts the text into a string of glyph ids, ready to be shown with the Tj operator
        /// </summary>
        public override string EncodeText(string text)
        {
            Used = true;

            var bytes = new List<byte>();
            foreach (var rune in (text ?? "").EnumerateRunes())
            {
                var gid = GetGlyph(rune);
                if (gid <= 0) continue;

                bytes.Add((byte)(gid >> 8));
                bytes.Add((byte)gid);

                if (!_usedGlyphs.ContainsKey(gid))
                {
                    _usedGlyphs[gid] = rune.ToString();
                }
            }

            return PdfString.Hex(bytes.ToArray());
        }

        internal override int AssignObjectNumbers(int nextObjectNumber)
        {
            ObjectNumber = nextObjectNumber;
            return nextObjectNumber + OBJECTCOUNT;
        }

        private int CidFontNumber => ObjectNumber + 1;
        private int ToUnicodeNumber => ObjectNumber + 4;

        private string SubsetFontName => PdfString.Name(SubsetTag() + "+" + (_ttf.PostScriptName ?? FontName));

        /// <summary>
        /// The Type0 font that the content streams refer to
        /// </summary>
        protected override Dictionary<string, dynamic> FontDictionary()
        {
            return new Dictionary<string, dynamic>
            {
                { "/Type", "/Font" },
                { "/Subtype", "/Type0" },
                { "/Name", "/" + Id },
                { "/BaseFont", "/" + SubsetFontName },
                { "/Encoding", "/Identity-H" },
                { "/DescendantFonts", string.Format("[{0} 0 R]", CidFontNumber) },
                { "/ToUnicode", string.Format("{0} 0 R", ToUnicodeNumber) }
            };
        }

        public override void Publish(StreamWriter stream)
        {
            var cidFontNumber = CidFontNumber;
            var descriptorNumber = ObjectNumber + 2;
            var fontFileNumber = ObjectNumber + 3;
            var toUnicodeNumber = ToUnicodeNumber;
            var fontName = SubsetFontName;

            base.Publish(stream);

            // The CID font that holds the glyph widths
            new RawObject(cidFontNumber, new Dictionary<string, dynamic>
            {
                { "/Type", "/Font" },
                { "/Subtype", "/CIDFontType2" },
                { "/BaseFont", "/" + fontName },
                { "/CIDSystemInfo", "<< /Registry (Adobe) /Ordering (Identity) /Supplement 0 >>" },
                { "/FontDescriptor", string.Format("{0} 0 R", descriptorNumber) },
                { "/DW", GlyphWidth(0) },
                { "/W", WidthsArray() },
                { "/CIDToGIDMap", "/Identity" }
            }).Publish(stream);

            // The descriptor of the font's overall metrics
            var flags = 4; // symbolic: the font is not limited to the standard Latin character set
            if (_ttf.IsFixedPitch) flags |= 1;
            if (_ttf.ItalicAngle != 0 || Italic) flags |= 64;

            new RawObject(descriptorNumber, new Dictionary<string, dynamic>
            {
                { "/Type", "/FontDescriptor" },
                { "/FontName", "/" + fontName },
                { "/Flags", flags },
                { "/FontBBox", string.Format("[{0} {1} {2} {3}]", _ttf.ToPdfUnits(_ttf.XMin), _ttf.ToPdfUnits(_ttf.YMin), _ttf.ToPdfUnits(_ttf.XMax), _ttf.ToPdfUnits(_ttf.YMax)) },
                { "/ItalicAngle", Math.Round(_ttf.ItalicAngle, 2).ToString(CultureInfo.InvariantCulture) },
                { "/Ascent", _ttf.ToPdfUnits(_ttf.Ascender) },
                { "/Descent", _ttf.ToPdfUnits(_ttf.Descender) },
                { "/CapHeight", _ttf.ToPdfUnits(_ttf.CapHeight) },
                { "/XHeight", _ttf.ToPdfUnits(_ttf.XHeight) },
                { "/StemV", EstimateStemV() },
                { "/FontWeight", _ttf.WeightClass },
                { "/FontFile2", string.Format("{0} 0 R", fontFileNumber) }
            }).Publish(stream);

            // The (subset) font program itself. Font data is always compressed as it is
            // binary data that compresses well
            var fontData = _ttf.Subset(_usedGlyphs.Keys);
            new RawObject(fontFileNumber, new Dictionary<string, dynamic>
            {
                { "/Length1", fontData.Length }
            }, fontData, compress: true).Publish(stream);

            // The map back from glyphs to Unicode text for searching and copying
            new RawObject(toUnicodeNumber, new Dictionary<string, dynamic>(),
                System.Text.Encoding.ASCII.GetBytes(ToUnicodeCMap()), compress: true).Publish(stream);
        }

        private int GetGlyph(Rune rune)
        {
            if (FontText.IsNeverShown(rune.Value)) return -1;

            var gid = _ttf.GetGlyphId(rune.Value);
            if (gid > 0) return gid;

            // fall back to a normal space if the font doesn't have a non-breaking one
            if (rune.Value == 0x00A0)
            {
                gid = _ttf.GetGlyphId(0x0020);
                if (gid > 0) return gid;
            }

            if (FontText.IsIgnorable(rune.Value)) return -1;

            throw new ArgumentException(string.Format(CultureInfo.InvariantCulture,
                "The character '{0}' (U+{1:X4}) is not available in the embedded font '{2}'.",
                rune.ToString(), rune.Value, FontName));
        }

        private int GlyphWidth(int gid)
        {
            return _ttf.ToPdfUnits(_ttf.GetAdvanceWidth(gid));
        }

        private string WidthsArray()
        {
            // written as runs of consecutive glyphs: [ first [w1 w2 ...] first [w1 ...] ]
            var result = new StringBuilder("[");
            var previous = -2;
            foreach (var gid in _usedGlyphs.Keys)
            {
                if (gid != previous + 1)
                {
                    if (previous >= 0) result.Append(']');
                    result.Append(' ').Append(gid).Append(" [");
                }
                else
                {
                    result.Append(' ');
                }
                result.Append(GlyphWidth(gid));
                previous = gid;
            }
            if (previous >= 0) result.Append(']');
            return result.Append(" ]").ToString();
        }

        private string ToUnicodeCMap()
        {
            var result = new StringBuilder();
            result.Append("/CIDInit /ProcSet findresource begin\n");
            result.Append("12 dict begin\n");
            result.Append("begincmap\n");
            result.Append("/CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def\n");
            result.Append("/CMapName /Adobe-Identity-UCS def\n");
            result.Append("/CMapType 2 def\n");
            result.Append("1 begincodespacerange\n<0000> <FFFF>\nendcodespacerange\n");

            var entries = _usedGlyphs.ToList();
            for (var i = 0; i < entries.Count; i += 100)
            {
                // a maximum of 100 entries are allowed in each block
                var block = entries.Skip(i).Take(100).ToList();
                result.AppendFormat(CultureInfo.InvariantCulture, "{0} beginbfchar\n", block.Count);
                foreach (var entry in block)
                {
                    result.AppendFormat(CultureInfo.InvariantCulture, "<{0:X4}> <{1}>\n",
                        entry.Key, Convert.ToHexString(System.Text.Encoding.BigEndianUnicode.GetBytes(entry.Value)));
                }
                result.Append("endbfchar\n");
            }

            result.Append("endcmap\n");
            result.Append("CMapName currentdict /CMap defineresource pop\n");
            result.Append("end\n");
            result.Append("end\n");
            return result.ToString();
        }

        /// <summary>
        /// Subset fonts must be named with a six letter tag. It is derived from the glyphs used
        /// so the same document always produces the same output.
        /// </summary>
        private string SubsetTag()
        {
            var key = (_ttf.PostScriptName ?? FontName) + ":" + string.Join(",", _usedGlyphs.Keys);
            var hash = MD5.HashData(System.Text.Encoding.UTF8.GetBytes(key));

            var tag = new char[6];
            for (var i = 0; i < 6; i++)
            {
                tag[i] = (char)('A' + (hash[i] % 26));
            }
            return new string(tag);
        }

        private int EstimateStemV()
        {
            // TrueType fonts don't record their stem width, so estimate it from the font weight
            return (int)Math.Round(10 + (220 * Math.Max(0, _ttf.WeightClass - 50) / 900.0));
        }
    }
}
