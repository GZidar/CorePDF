using CorePDF.TypeFaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;

namespace CorePDF.UnitTests
{
    [Trait("Category", "Unit Test")]
    public class TrueTypeFontTests
    {
        private static readonly byte[] _fontData = File.ReadAllBytes("GreatVibes-Regular.ttf");
        private readonly TrueTypeFont _sut = new TrueTypeFont(_fontData);

        [Fact]
        public void Parse_ReadsMetricsFromTheFile()
        {
            Assert.Equal(1000, _sut.UnitsPerEm);
            Assert.True(_sut.NumGlyphs > 100);
            Assert.Equal("GreatVibes-Regular", _sut.PostScriptName);
            Assert.True(_sut.Ascender > 0);
            Assert.True(_sut.Descender < 0);
            Assert.True(_sut.XMax > _sut.XMin);
            Assert.True(_sut.YMax > _sut.YMin);
            Assert.False(_sut.EmbeddingRestricted);
        }

        [Theory]
        [InlineData('A')]
        [InlineData('z')]
        [InlineData('ż')] // ż - outside WinAnsi
        [InlineData('€')] // €
        public void GetGlyphId_CharacterInFont_ReturnsGlyphWithWidth(char c)
        {
            var gid = _sut.GetGlyphId(c);

            Assert.True(gid > 0);
            Assert.True(_sut.GetAdvanceWidth(gid) > 0);
        }

        [Fact]
        public void GetGlyphId_CharacterNotInFont_ReturnsZero()
        {
            Assert.Equal(0, _sut.GetGlyphId(0x041A)); // Cyrillic K
        }

        [Fact]
        public void Subset_ProducesValidSmallerFontWithSameGlyphIds()
        {
            var used = "Hello".Select(c => _sut.GetGlyphId(c)).ToList();

            var subset = _sut.Subset(used);
            var parsed = new TrueTypeFont(subset);

            Assert.True(subset.Length < _fontData.Length / 2, $"subset is {subset.Length} bytes, original {_fontData.Length}");
            Assert.Equal(_sut.NumGlyphs, parsed.NumGlyphs);
            Assert.Equal(_sut.UnitsPerEm, parsed.UnitsPerEm);

            foreach (var gid in used)
            {
                Assert.Equal(Padded(_sut.GetGlyphDataLength(gid)), parsed.GetGlyphDataLength(gid));
                Assert.Equal(_sut.GetAdvanceWidth(gid), parsed.GetAdvanceWidth(gid));
            }

            // a glyph that wasn't used has had its outline removed
            var unused = _sut.GetGlyphId('Q');
            Assert.True(_sut.GetGlyphDataLength(unused) > 0);
            Assert.Equal(0, parsed.GetGlyphDataLength(unused));
        }

        [Fact]
        public void Subset_HasValidWholeFileChecksum()
        {
            var subset = _sut.Subset(new[] { _sut.GetGlyphId('A') });

            uint sum = 0;
            for (var i = 0; i < subset.Length; i += 4)
            {
                sum = unchecked(sum + (uint)((subset[i] << 24) | (subset[i + 1] << 16) | (subset[i + 2] << 8) | subset[i + 3]));
            }

            Assert.Equal(0, subset.Length % 4);
            Assert.Equal(0xB1B0AFBA, sum);
        }

        [Fact]
        public void Subset_KeepsTheComponentsOfCompositeGlyphs()
        {
            // Great Vibes has no composite glyphs, so use a tiny font where glyph 3 is built
            // from glyphs 1 and 2 (like an accented letter built from a letter and an accent)
            var font = new TrueTypeFont(BuildFontWithCompositeGlyph());
            Assert.Equal(new[] { 1, 2 }, font.GetComponentGlyphs(3));
            Assert.Empty(font.GetComponentGlyphs(1));

            var parsed = new TrueTypeFont(font.Subset(new[] { 3 }));

            Assert.Equal(Padded(font.GetGlyphDataLength(1)), parsed.GetGlyphDataLength(1));
            Assert.Equal(Padded(font.GetGlyphDataLength(2)), parsed.GetGlyphDataLength(2));
            Assert.Equal(Padded(font.GetGlyphDataLength(3)), parsed.GetGlyphDataLength(3));
            Assert.Equal(0, parsed.GetGlyphDataLength(4)); // unused
        }

        /// <summary>
        /// Glyphs in a subset font are padded to a four byte boundary
        /// </summary>
        private static int Padded(int length) => (length + 3) & ~3;

        /// <summary>
        /// Builds a minimal TrueType font with five glyphs: an empty .notdef, three simple
        /// glyphs (1, 2 and 4) and a composite glyph (3) made from glyphs 1 and 2.
        /// </summary>
        private static byte[] BuildFontWithCompositeGlyph()
        {
            static byte[] Bytes(params int[] words) => words.SelectMany(w => new[] { (byte)(w >> 8), (byte)w }).ToArray();

            // a one point simple glyph: contours, bbox, end point, no instructions, flag, x, y (+ padding)
            var simple = Bytes(1, 0, 0, 10, 10, 0, 0).Concat(new byte[] { 0x01, 0, 5, 0, 5, 0 }).ToArray();
            // a composite glyph: -1 contours, bbox, then two components with word offsets
            var composite = Bytes(0xFFFF, 0, 0, 10, 10, 0x0021, 1, 0, 0, 0x0001, 2, 0, 0);
            var glyphs = new[] { new byte[0], simple, simple, composite, simple };

            var glyf = glyphs.SelectMany(g => g).ToArray();
            var loca = new List<byte>();
            var offset = 0;
            foreach (var glyph in glyphs)
            {
                loca.AddRange(new[] { (byte)(offset >> 24), (byte)(offset >> 16), (byte)(offset >> 8), (byte)offset });
                offset += glyph.Length;
            }
            loca.AddRange(new[] { (byte)(offset >> 24), (byte)(offset >> 16), (byte)(offset >> 8), (byte)offset });

            var head = new byte[54];
            Bytes(1, 0).CopyTo(head, 0); // version
            Bytes(0x5F0F, 0x3CF5).CopyTo(head, 12); // magic number
            Bytes(1000).CopyTo(head, 18); // units per em
            Bytes(0, 0, 10, 10).CopyTo(head, 36); // bounding box
            Bytes(1).CopyTo(head, 50); // long loca offsets

            var hhea = new byte[36];
            Bytes(1, 0, 800, 0xFF38).CopyTo(hhea, 0); // version, ascender, descender
            Bytes(5).CopyTo(hhea, 34); // number of horizontal metrics

            var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
            {
                { "glyf", glyf },
                { "head", head },
                { "hhea", hhea },
                { "hmtx", Bytes(500, 0, 500, 0, 500, 0, 500, 0, 500, 0) },
                { "loca", loca.ToArray() },
                { "maxp", Bytes(0, 0x5000, 5) }
            };

            var result = new List<byte>(Bytes(1, 0, tables.Count, 64, 2, 32));
            var dataOffset = 12 + (tables.Count * 16);
            var data = new List<byte>();
            foreach (var table in tables)
            {
                result.AddRange(System.Text.Encoding.ASCII.GetBytes(table.Key));
                result.AddRange(Bytes(0, 0)); // checksum (not checked when reading)
                var start = dataOffset + data.Count;
                result.AddRange(Bytes(start >> 16, start & 0xFFFF, table.Value.Length >> 16, table.Value.Length & 0xFFFF));
                data.AddRange(table.Value);
                while (data.Count % 4 != 0) data.Add(0);
            }
            result.AddRange(data);
            return result.ToArray();
        }

        [Fact]
        public void Parse_NotAFont_Throws()
        {
            Assert.Throws<InvalidDataException>(() => new TrueTypeFont(new byte[] { 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11, 12 }));
        }

        [Fact]
        public void Parse_TruncatedFont_Throws()
        {
            Assert.Throws<InvalidDataException>(() => new TrueTypeFont(_fontData.Take(200).ToArray()));
        }

        [Fact]
        public void Parse_PostScriptOutlines_ThrowsNotSupported()
        {
            var otf = new byte[64];
            "OTTO"u8.ToArray().CopyTo(otf, 0);

            var ex = Assert.Throws<NotSupportedException>(() => new TrueTypeFont(otf));
            Assert.Contains("CFF", ex.Message);
        }
    }
}
