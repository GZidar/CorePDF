using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CorePDF.TypeFaces
{
    /// <summary>
    /// A minimal reader for TrueType (glyf outline) font files. It reads just enough of the
    /// font to measure text, map characters to glyphs, describe the font to a PDF reader and
    /// produce a subset of the font containing only the glyphs that were used.
    /// </summary>
    internal sealed class TrueTypeFont
    {
        private readonly byte[] _data;
        private readonly Dictionary<string, TableRecord> _tables = new Dictionary<string, TableRecord>();
        private readonly Dictionary<int, int> _cmap = new Dictionary<int, int>();
        private bool _symbolCmap;
        private ushort[] _advanceWidths;
        private uint[] _loca;

        private struct TableRecord
        {
            public int Offset;
            public int Length;
        }

        public int UnitsPerEm { get; private set; }
        public int NumGlyphs { get; private set; }
        public short XMin { get; private set; }
        public short YMin { get; private set; }
        public short XMax { get; private set; }
        public short YMax { get; private set; }
        public short Ascender { get; private set; }
        public short Descender { get; private set; }
        public short CapHeight { get; private set; }
        public short XHeight { get; private set; }
        public int WeightClass { get; private set; } = 400;
        public double ItalicAngle { get; private set; }
        public bool IsFixedPitch { get; private set; }
        public ushort EmbeddingFlags { get; private set; }
        public string PostScriptName { get; private set; }

        public TrueTypeFont(byte[] data)
        {
            _data = data ?? throw new ArgumentNullException(nameof(data));

            try
            {
                Parse();
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException || ex is ArgumentOutOfRangeException || ex is KeyNotFoundException)
            {
                throw new InvalidDataException("The font file is not a valid TrueType font.", ex);
            }
        }

        /// <summary>
        /// True if the font licence forbids embedding (the OS/2 "restricted licence" flag on its own)
        /// </summary>
        public bool EmbeddingRestricted => (EmbeddingFlags & 0x000F) == 0x0002;

        /// <summary>
        /// Returns the glyph used for the Unicode code point, or zero if the font has no glyph for it.
        /// </summary>
        public int GetGlyphId(int codePoint)
        {
            if (_cmap.TryGetValue(codePoint, out var gid)) return gid;

            // symbol fonts map their characters into the private use area at U+F000
            if (_symbolCmap && codePoint < 0x100 && _cmap.TryGetValue(0xF000 + codePoint, out gid)) return gid;

            return 0;
        }

        /// <summary>
        /// The advance width of the glyph in font units
        /// </summary>
        public int GetAdvanceWidth(int glyphId)
        {
            if (_advanceWidths.Length == 0) return 0;
            if (glyphId >= _advanceWidths.Length)
            {
                // glyphs after the last horizontal metric share its width
                return _advanceWidths[_advanceWidths.Length - 1];
            }
            return _advanceWidths[glyphId];
        }

        /// <summary>
        /// Scales a value in font units to the 1000 unit em square used by PDF
        /// </summary>
        public int ToPdfUnits(int value)
        {
            return (int)Math.Round(value * 1000.0 / UnitsPerEm, MidpointRounding.AwayFromZero);
        }

        #region Parsing

        private void Parse()
        {
            var fontOffset = 0;
            var tag = ReadTag(0);

            if (tag == "ttcf")
            {
                // a font collection - use the first font in it
                fontOffset = (int)ReadUInt32(12);
                tag = ReadTag(fontOffset);
            }

            if (tag == "OTTO")
            {
                throw new NotSupportedException("OpenType fonts with PostScript (CFF) outlines are not supported. Please use a font with TrueType outlines.");
            }
            if (ReadUInt32(fontOffset) != 0x00010000 && tag != "true")
            {
                throw new InvalidDataException("The font file is not a valid TrueType font.");
            }

            var numTables = ReadUInt16(fontOffset + 4);
            for (var i = 0; i < numTables; i++)
            {
                var record = fontOffset + 12 + (i * 16);
                _tables[ReadTag(record)] = new TableRecord
                {
                    Offset = (int)ReadUInt32(record + 8),
                    Length = (int)ReadUInt32(record + 12)
                };
            }

            foreach (var required in new[] { "head", "hhea", "hmtx", "maxp", "loca", "glyf" })
            {
                if (!_tables.ContainsKey(required))
                {
                    throw new InvalidDataException($"The font file is missing the required '{required}' table.");
                }
            }

            var head = _tables["head"].Offset;
            UnitsPerEm = ReadUInt16(head + 18);
            XMin = ReadInt16(head + 36);
            YMin = ReadInt16(head + 38);
            XMax = ReadInt16(head + 40);
            YMax = ReadInt16(head + 42);
            var indexToLocFormat = ReadInt16(head + 50);

            if (UnitsPerEm == 0)
            {
                throw new InvalidDataException("The font file has an invalid units per em value.");
            }

            NumGlyphs = ReadUInt16(_tables["maxp"].Offset + 4);

            var hhea = _tables["hhea"].Offset;
            Ascender = ReadInt16(hhea + 4);
            Descender = ReadInt16(hhea + 6);
            var numberOfHMetrics = Math.Min((int)ReadUInt16(hhea + 34), NumGlyphs);

            var hmtx = _tables["hmtx"].Offset;
            _advanceWidths = new ushort[numberOfHMetrics];
            for (var i = 0; i < numberOfHMetrics; i++)
            {
                _advanceWidths[i] = ReadUInt16(hmtx + (i * 4));
            }

            var loca = _tables["loca"].Offset;
            _loca = new uint[NumGlyphs + 1];
            for (var i = 0; i <= NumGlyphs; i++)
            {
                _loca[i] = indexToLocFormat == 0 ? (uint)ReadUInt16(loca + (i * 2)) * 2 : ReadUInt32(loca + (i * 4));
            }

            // sensible fallbacks for fonts without an OS/2 table (or an old version of one)
            CapHeight = Ascender;
            XHeight = (short)(Ascender / 2);

            if (_tables.TryGetValue("OS/2", out var os2))
            {
                var version = ReadUInt16(os2.Offset);
                WeightClass = ReadUInt16(os2.Offset + 4);
                EmbeddingFlags = ReadUInt16(os2.Offset + 8);
                if (version >= 2 && os2.Length >= 90)
                {
                    XHeight = ReadInt16(os2.Offset + 86);
                    CapHeight = ReadInt16(os2.Offset + 88);
                }
            }

            if (_tables.TryGetValue("post", out var post))
            {
                ItalicAngle = ReadInt32(post.Offset + 4) / 65536.0;
                IsFixedPitch = ReadUInt32(post.Offset + 12) != 0;
            }

            PostScriptName = ReadPostScriptName();
            ParseCmap();
        }

        private void ParseCmap()
        {
            // subset fonts embedded in a PDF don't need a character map, as the PDF maps
            // characters to glyphs itself
            if (!_tables.ContainsKey("cmap")) return;

            var cmap = _tables["cmap"].Offset;
            var numSubtables = ReadUInt16(cmap + 2);

            // rank the subtables so the most complete Unicode mapping is used
            var bestRank = -1;
            var bestOffset = 0;
            for (var i = 0; i < numSubtables; i++)
            {
                var record = cmap + 4 + (i * 8);
                var platform = ReadUInt16(record);
                var encoding = ReadUInt16(record + 2);
                var offset = cmap + (int)ReadUInt32(record + 4);
                var format = ReadUInt16(offset);

                var rank = -1;
                if (format == 12 && (platform == 3 && encoding == 10 || platform == 0)) rank = 4;
                else if (format == 4 && (platform == 3 && encoding == 1 || platform == 0)) rank = 3;
                else if (format == 4 && platform == 3 && encoding == 0) rank = 2;
                else if ((format == 6 || format == 0) && (platform == 3 || platform == 0)) rank = 1;

                if (rank > bestRank)
                {
                    bestRank = rank;
                    bestOffset = offset;
                    _symbolCmap = platform == 3 && encoding == 0;
                }
            }

            if (bestRank < 0)
            {
                throw new NotSupportedException("The font does not contain a Unicode character map.");
            }

            switch (ReadUInt16(bestOffset))
            {
                case 0:
                    for (var c = 0; c < 256; c++)
                    {
                        AddMapping(c, _data[bestOffset + 6 + c]);
                    }
                    break;

                case 4:
                    var segCountX2 = ReadUInt16(bestOffset + 6);
                    var endCodes = bestOffset + 14;
                    var startCodes = endCodes + segCountX2 + 2;
                    var idDeltas = startCodes + segCountX2;
                    var idRangeOffsets = idDeltas + segCountX2;

                    for (var seg = 0; seg < segCountX2 / 2; seg++)
                    {
                        var end = ReadUInt16(endCodes + (seg * 2));
                        var start = ReadUInt16(startCodes + (seg * 2));
                        var delta = ReadInt16(idDeltas + (seg * 2));
                        var rangeOffsetPos = idRangeOffsets + (seg * 2);
                        var rangeOffset = ReadUInt16(rangeOffsetPos);

                        for (var c = start; c <= end && c != 0xFFFF; c++)
                        {
                            int gid;
                            if (rangeOffset == 0)
                            {
                                gid = (c + delta) & 0xFFFF;
                            }
                            else
                            {
                                var glyphPos = rangeOffsetPos + rangeOffset + ((c - start) * 2);
                                gid = ReadUInt16(glyphPos);
                                if (gid != 0) gid = (gid + delta) & 0xFFFF;
                            }
                            AddMapping(c, gid);
                        }
                    }
                    break;

                case 6:
                    var firstCode = ReadUInt16(bestOffset + 6);
                    var entryCount = ReadUInt16(bestOffset + 8);
                    for (var i = 0; i < entryCount; i++)
                    {
                        AddMapping(firstCode + i, ReadUInt16(bestOffset + 10 + (i * 2)));
                    }
                    break;

                case 12:
                    var numGroups = ReadUInt32(bestOffset + 12);
                    for (var g = 0; g < numGroups; g++)
                    {
                        var group = bestOffset + 16 + (g * 12);
                        var startChar = ReadUInt32(group);
                        var endChar = ReadUInt32(group + 4);
                        var startGlyph = ReadUInt32(group + 8);
                        for (var c = startChar; c <= endChar && c <= 0x10FFFF; c++)
                        {
                            AddMapping((int)c, (int)(startGlyph + (c - startChar)));
                        }
                    }
                    break;
            }
        }

        private void AddMapping(int codePoint, int glyphId)
        {
            if (glyphId > 0 && glyphId < NumGlyphs)
            {
                _cmap[codePoint] = glyphId;
            }
        }

        private string ReadPostScriptName()
        {
            if (!_tables.TryGetValue("name", out var name)) return null;

            var count = ReadUInt16(name.Offset + 2);
            var stringOffset = name.Offset + ReadUInt16(name.Offset + 4);

            for (var i = 0; i < count; i++)
            {
                var record = name.Offset + 6 + (i * 12);
                var platform = ReadUInt16(record);
                var nameId = ReadUInt16(record + 6);
                var length = ReadUInt16(record + 8);
                var offset = stringOffset + ReadUInt16(record + 10);

                if (nameId != 6) continue;

                var value = platform == 1
                    ? Encoding.ASCII.GetString(_data, offset, length)
                    : Encoding.BigEndianUnicode.GetString(_data, offset, length);

                // PostScript names are restricted to printable ASCII without delimiters
                var clean = new string(value.Where(c => c > 32 && c < 127 && "[](){}<>/%".IndexOf(c) < 0).ToArray());
                if (!string.IsNullOrEmpty(clean)) return clean;
            }

            return null;
        }

        #endregion

        #region Subsetting

        /// <summary>
        /// Creates a new font file that only contains the outlines of the requested glyphs
        /// (along with any glyphs they are built from). Glyph ids are left unchanged so the
        /// content of the document doesn't need to be renumbered, the outlines of unused
        /// glyphs are simply removed.
        /// </summary>
        public byte[] Subset(IEnumerable<int> glyphIds)
        {
            var keep = new HashSet<int> { 0 }; // .notdef must always be present
            var pending = new Stack<int>(glyphIds.Where(g => g > 0 && g < NumGlyphs));

            while (pending.Count > 0)
            {
                var gid = pending.Pop();
                if (!keep.Add(gid)) continue;

                foreach (var component in GetComponentGlyphs(gid))
                {
                    if (!keep.Contains(component)) pending.Push(component);
                }
            }

            // build the new glyph data and (long format) location table
            var glyf = _tables["glyf"].Offset;
            var newGlyf = new MemoryStream();
            var newLoca = new byte[(NumGlyphs + 1) * 4];

            for (var gid = 0; gid < NumGlyphs; gid++)
            {
                WriteUInt32(newLoca, gid * 4, (uint)newGlyf.Length);

                if (!keep.Contains(gid)) continue;

                var start = (int)_loca[gid];
                var length = (int)_loca[gid + 1] - start;
                if (length <= 0) continue;

                newGlyf.Write(_data, glyf + start, length);
                while (newGlyf.Length % 4 != 0) newGlyf.WriteByte(0);
            }
            WriteUInt32(newLoca, NumGlyphs * 4, (uint)newGlyf.Length);

            var head = Slice("head");
            WriteUInt32(head, 8, 0); // checkSumAdjustment - recalculated below
            WriteUInt16(head, 50, 1); // indexToLocFormat: long offsets

            var tables = new SortedDictionary<string, byte[]>(StringComparer.Ordinal)
            {
                { "head", head },
                { "hhea", Slice("hhea") },
                { "hmtx", Slice("hmtx") },
                { "maxp", Slice("maxp") },
                { "loca", newLoca },
                { "glyf", newGlyf.ToArray() }
            };

            // hinting programs and descriptive tables are kept, everything used for text
            // layout (cmap, kern, GSUB, etc) is dropped as the PDF does that itself
            foreach (var optional in new[] { "cvt ", "fpgm", "prep", "OS/2", "name" })
            {
                if (_tables.ContainsKey(optional))
                {
                    tables[optional] = Slice(optional);
                }
            }

            return WriteFontFile(tables);
        }

        /// <summary>
        /// The size of the glyph's outline data in bytes (zero for an empty glyph)
        /// </summary>
        internal int GetGlyphDataLength(int gid)
        {
            return (int)_loca[gid + 1] - (int)_loca[gid];
        }

        /// <summary>
        /// The glyphs a composite glyph is built from (none for a simple glyph)
        /// </summary>
        internal IEnumerable<int> GetComponentGlyphs(int gid)
        {
            var start = (int)_loca[gid];
            var length = (int)_loca[gid + 1] - start;
            if (length < 10) yield break;

            var pos = _tables["glyf"].Offset + start;
            if (ReadInt16(pos) >= 0) yield break; // simple glyph

            pos += 10;
            while (true)
            {
                var flags = ReadUInt16(pos);
                yield return ReadUInt16(pos + 2);
                pos += 4;

                pos += (flags & 0x0001) != 0 ? 4 : 2; // ARG_1_AND_2_ARE_WORDS
                if ((flags & 0x0008) != 0) pos += 2; // WE_HAVE_A_SCALE
                else if ((flags & 0x0040) != 0) pos += 4; // WE_HAVE_AN_X_AND_Y_SCALE
                else if ((flags & 0x0080) != 0) pos += 8; // WE_HAVE_A_TWO_BY_TWO

                if ((flags & 0x0020) == 0) break; // MORE_COMPONENTS
            }
        }

        private static byte[] WriteFontFile(SortedDictionary<string, byte[]> tables)
        {
            var numTables = tables.Count;
            var entrySelector = 0;
            while ((1 << (entrySelector + 1)) <= numTables) entrySelector++;
            var searchRange = (1 << entrySelector) * 16;

            var headerLength = 12 + (numTables * 16);
            var output = new MemoryStream();
            var header = new byte[headerLength];

            WriteUInt32(header, 0, 0x00010000);
            WriteUInt16(header, 4, numTables);
            WriteUInt16(header, 6, searchRange);
            WriteUInt16(header, 8, entrySelector);
            WriteUInt16(header, 10, (numTables * 16) - searchRange);
            output.Write(header, 0, header.Length);

            var headOffset = 0;
            var index = 0;
            foreach (var table in tables)
            {
                var record = 12 + (index * 16);
                var offset = (int)output.Length;
                if (table.Key == "head") headOffset = offset;

                Encoding.ASCII.GetBytes(table.Key, 0, 4, header, record);
                WriteUInt32(header, record + 4, Checksum(table.Value));
                WriteUInt32(header, record + 8, (uint)offset);
                WriteUInt32(header, record + 12, (uint)table.Value.Length);

                output.Write(table.Value, 0, table.Value.Length);
                while (output.Length % 4 != 0) output.WriteByte(0);
                index++;
            }

            var result = output.ToArray();
            Array.Copy(header, result, header.Length);

            // the whole font checksum is stored in the head table
            WriteUInt32(result, headOffset + 8, unchecked(0xB1B0AFBA - Checksum(result)));

            return result;
        }

        private static uint Checksum(byte[] data)
        {
            uint sum = 0;
            for (var i = 0; i < data.Length; i += 4)
            {
                uint value = 0;
                for (var j = 0; j < 4; j++)
                {
                    value <<= 8;
                    if (i + j < data.Length) value |= data[i + j];
                }
                sum = unchecked(sum + value);
            }
            return sum;
        }

        private byte[] Slice(string tag)
        {
            var table = _tables[tag];
            var result = new byte[table.Length];
            Array.Copy(_data, table.Offset, result, 0, table.Length);
            return result;
        }

        #endregion

        #region Binary helpers

        private string ReadTag(int pos) => Encoding.ASCII.GetString(_data, pos, 4);
        private ushort ReadUInt16(int pos) => (ushort)((_data[pos] << 8) | _data[pos + 1]);
        private short ReadInt16(int pos) => (short)ReadUInt16(pos);
        private uint ReadUInt32(int pos) => ((uint)_data[pos] << 24) | ((uint)_data[pos + 1] << 16) | ((uint)_data[pos + 2] << 8) | _data[pos + 3];
        private int ReadInt32(int pos) => (int)ReadUInt32(pos);

        private static void WriteUInt16(byte[] data, int pos, int value)
        {
            data[pos] = (byte)(value >> 8);
            data[pos + 1] = (byte)value;
        }

        private static void WriteUInt32(byte[] data, int pos, uint value)
        {
            data[pos] = (byte)(value >> 24);
            data[pos + 1] = (byte)(value >> 16);
            data[pos + 2] = (byte)(value >> 8);
            data[pos + 3] = (byte)value;
        }

        #endregion
    }
}
