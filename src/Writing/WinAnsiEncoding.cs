using System.Collections.Generic;

namespace CorePDF.Writing
{
    /// <summary>
    /// Maps Unicode code points to the single byte WinAnsiEncoding (Windows code page 1252)
    /// used by the standard PDF fonts.
    /// </summary>
    internal static class WinAnsiEncoding
    {
        private static readonly Dictionary<int, byte> _special = new Dictionary<int, byte>
        {
            { 0x20AC, 128 }, { 0x201A, 130 }, { 0x0192, 131 }, { 0x201E, 132 }, { 0x2026, 133 },
            { 0x2020, 134 }, { 0x2021, 135 }, { 0x02C6, 136 }, { 0x2030, 137 }, { 0x0160, 138 },
            { 0x2039, 139 }, { 0x0152, 140 }, { 0x017D, 142 }, { 0x2018, 145 }, { 0x2019, 146 },
            { 0x201C, 147 }, { 0x201D, 148 }, { 0x2022, 149 }, { 0x2013, 150 }, { 0x2014, 151 },
            { 0x02DC, 152 }, { 0x2122, 153 }, { 0x0161, 154 }, { 0x203A, 155 }, { 0x0153, 156 },
            { 0x017E, 158 }, { 0x0178, 159 }
        };

        /// <summary>
        /// Returns the WinAnsi byte for the code point, or false if it cannot be represented.
        /// </summary>
        public static bool TryEncode(int codePoint, out byte value)
        {
            if ((codePoint >= 0x20 && codePoint <= 0x7E) || (codePoint >= 0xA0 && codePoint <= 0xFF))
            {
                value = (byte)codePoint;
                return true;
            }

            return _special.TryGetValue(codePoint, out value);
        }
    }
}
