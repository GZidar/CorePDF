using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CorePDF.IntegrationTests.Svg
{
    /// <summary>
    /// Fonts for the SVG text accuracy tests
    /// </summary>
    internal static class TestFonts
    {
        public const string FAMILY = "Great Vibes";

        private static readonly Lazy<string> _plain = new Lazy<string>(CreatePlain);

        /// <summary>
        /// A copy of Great Vibes without its OpenType layout tables (GSUB, GPOS and GDEF). The
        /// reference renderer shapes text (applying ligatures, contextual alternates and kerning
        /// from those tables) while CorePDF uses each character's own glyph, so without them both
        /// draw the same glyphs and the comparison measures text layout rather than shaping.
        /// </summary>
        public static string Plain => _plain.Value;

        private static string CreatePlain()
        {
            var source = File.ReadAllBytes("GreatVibes-Regular.ttf");
            var path = Path.Combine(AppContext.BaseDirectory, "GreatVibes-Plain.ttf");
            File.WriteAllBytes(path, WithoutTables(source, "GSUB", "GPOS", "GDEF"));
            return path;
        }

        private static byte[] WithoutTables(byte[] font, params string[] remove)
        {
            int U16(int pos) => (font[pos] << 8) | font[pos + 1];
            int U32(int pos) => (font[pos] << 24) | (font[pos + 1] << 16) | (font[pos + 2] << 8) | font[pos + 3];

            var tables = new List<(string Tag, uint Checksum, byte[] Data)>();
            var count = U16(4);
            for (var i = 0; i < count; i++)
            {
                var record = 12 + (i * 16);
                var tag = Encoding.ASCII.GetString(font, record, 4);
                if (Array.IndexOf(remove, tag) >= 0) continue;

                var data = new byte[U32(record + 12)];
                Array.Copy(font, U32(record + 8), data, 0, data.Length);
                tables.Add((tag, (uint)U32(record + 4), data));
            }

            var output = new MemoryStream();
            void W16(int value) { output.WriteByte((byte)(value >> 8)); output.WriteByte((byte)value); }
            void W32(long value) { W16((int)(value >> 16) & 0xFFFF); W16((int)value & 0xFFFF); }

            var entrySelector = 0;
            while ((1 << (entrySelector + 1)) <= tables.Count) entrySelector++;
            W32(0x00010000);
            W16(tables.Count);
            W16((1 << entrySelector) * 16);
            W16(entrySelector);
            W16((tables.Count * 16) - ((1 << entrySelector) * 16));

            long offset = 12 + (tables.Count * 16);
            foreach (var (tag, checksum, data) in tables)
            {
                output.Write(Encoding.ASCII.GetBytes(tag));
                W32(checksum);
                W32(offset);
                W32(data.Length);
                offset += (data.Length + 3) & ~3;
            }
            foreach (var (_, _, data) in tables)
            {
                output.Write(data);
                while (output.Length % 4 != 0) output.WriteByte(0);
            }
            return output.ToArray();
        }
    }
}
