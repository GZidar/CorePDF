using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace CorePDF.IntegrationTests
{
    /// <summary>
    /// A deliberately small PDF reader for tests. It locates every object through the cross
    /// reference table (so a wrong offset fails loudly), decodes streams, and can turn the
    /// text shown on a page back into Unicode using each font's encoding or ToUnicode map.
    /// It only understands the subset of PDF that CorePDF writes.
    /// </summary>
    public class PdfInspector
    {
        private static readonly Encoding Latin1 = Encoding.Latin1;

        public byte[] Data { get; }
        public string Header { get; }
        public string Trailer { get; }
        public Dictionary<int, PdfObjectInfo> Objects { get; } = new Dictionary<int, PdfObjectInfo>();

        public PdfInspector(byte[] data)
        {
            Data = data;
            var text = Latin1.GetString(data);
            Header = text.Substring(0, text.IndexOf('\n'));

            var startXref = int.Parse(Regex.Match(text, @"startxref\n(\d+)\n%%EOF$").Groups[1].Value);
            if (!text.Substring(startXref).StartsWith("xref\n"))
            {
                throw new InvalidDataException("startxref does not point at the cross reference table.");
            }

            var subsection = Regex.Match(text.Substring(startXref), @"^xref\n0 (\d+)\n");
            var count = int.Parse(subsection.Groups[1].Value);
            var entriesStart = startXref + subsection.Length;

            for (var i = 0; i < count; i++)
            {
                var entry = text.Substring(entriesStart + (i * 20), 20);
                if (!Regex.IsMatch(entry, @"^\d{10} \d{5} [nf] \n$"))
                {
                    throw new InvalidDataException($"Cross reference entry {i} is not 20 bytes in the expected format: '{entry}'");
                }
                if (i == 0) continue;

                var offset = int.Parse(entry.Substring(0, 10));
                Objects[i] = ReadObject(text, i, offset);
            }

            Trailer = text.Substring(entriesStart + (count * 20));
        }

        private PdfObjectInfo ReadObject(string text, int number, int offset)
        {
            var prefix = number + " 0 obj";
            if (string.CompareOrdinal(text, offset, prefix, 0, prefix.Length) != 0)
            {
                throw new InvalidDataException($"The cross reference offset for object {number} does not point at the object.");
            }

            var bodyStart = text.IndexOf('\n', offset) + 1;
            var end = text.IndexOf("endobj", bodyStart, StringComparison.Ordinal);
            var streamPos = text.IndexOf("stream\n", bodyStart, StringComparison.Ordinal);

            var result = new PdfObjectInfo { Number = number };
            if (streamPos >= 0 && streamPos < end)
            {
                result.Dictionary = text.Substring(bodyStart, streamPos - bodyStart);
                var length = int.Parse(Regex.Match(result.Dictionary, @"/Length (\d+)").Groups[1].Value);
                var raw = new byte[length];
                Array.Copy(Data, streamPos + 7, raw, 0, length);

                if (text.Substring(streamPos + 7 + length, 11) != "\nendstream\n")
                {
                    throw new InvalidDataException($"The /Length of object {number} doesn't match its stream.");
                }

                result.Stream = result.Dictionary.Contains("/FlateDecode") ? Inflate(raw) : raw;
            }
            else
            {
                result.Dictionary = text.Substring(bodyStart, end - bodyStart);
            }

            return result;
        }

        private static byte[] Inflate(byte[] data)
        {
            using (var input = new ZLibStream(new MemoryStream(data), CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                input.CopyTo(output);
                return output.ToArray();
            }
        }

        public PdfObjectInfo Resolve(string reference)
        {
            return Objects[int.Parse(Regex.Match(reference, @"(\d+) 0 R").Groups[1].Value)];
        }

        public PdfObjectInfo Catalog => Resolve(Regex.Match(Trailer, @"/Root (\d+ 0 R)").Groups[1].Value);

        public PdfObjectInfo Info => Resolve(Regex.Match(Trailer, @"/Info (\d+ 0 R)").Groups[1].Value);

        public List<PdfObjectInfo> Pages => Objects.Values.Where(o => Regex.IsMatch(o.Dictionary, @"/Type /Page\b")).OrderBy(o => o.Number).ToList();

        public List<PdfObjectInfo> Fonts => Objects.Values.Where(o => Regex.IsMatch(o.Dictionary, @"/Type /Font\s") && !o.Dictionary.Contains("/CIDFontType2")).ToList();

        /// <summary>
        /// The text shown on the page, one entry per line (each Td starts a new line)
        /// </summary>
        public List<string> PageText(PdfObjectInfo page)
        {
            var fonts = new Dictionary<string, Func<string, string>>();
            var fontDict = Regex.Match(page.Dictionary, @"/Font <<([^>]*)>>");
            foreach (Match m in Regex.Matches(fontDict.Groups[1].Value, @"/(\w+) (\d+ 0 R)"))
            {
                fonts[m.Groups[1].Value] = Decoder(Resolve(m.Groups[2].Value));
            }

            var lines = new List<string>();
            var contents = Regex.Match(page.Dictionary, @"/Contents \[([^\]]*)\]").Groups[1].Value;
            foreach (Match reference in Regex.Matches(contents, @"\d+ 0 R"))
            {
                var stream = Latin1.GetString(Resolve(reference.Value).Stream ?? new byte[0]);
                Func<string, string> decoder = null;
                StringBuilder current = null;

                foreach (Match op in Regex.Matches(stream, @"/(\w+) [\d.]+ Tf|(-?[\d.]+) (-?[\d.]+) Td|(\((?:\\.|[^\\)])*\)|<[0-9A-Fa-f]*>) Tj|\bET\b"))
                {
                    if (op.Groups[1].Success)
                    {
                        decoder = fonts[op.Groups[1].Value];
                    }
                    else if (op.Groups[2].Success)
                    {
                        current = new StringBuilder();
                        lines.Add(null);
                    }
                    else if (op.Groups[4].Success)
                    {
                        current.Append(decoder(op.Groups[4].Value));
                        lines[lines.Count - 1] = current.ToString();
                    }
                }
            }

            return lines.Select(l => l ?? "").ToList();
        }

        private Func<string, string> Decoder(PdfObjectInfo font)
        {
            if (font.Dictionary.Contains("/Subtype /Type0"))
            {
                // two byte glyph ids mapped through the font's ToUnicode CMap
                var cmap = Latin1.GetString(Resolve(Regex.Match(font.Dictionary, @"/ToUnicode (\d+ 0 R)").Groups[1].Value).Stream);
                var map = new Dictionary<int, string>();
                foreach (Match m in Regex.Matches(cmap, @"<([0-9A-F]{4})> <([0-9A-F]+)>"))
                {
                    map[int.Parse(m.Groups[1].Value, NumberStyles.HexNumber)] = Encoding.BigEndianUnicode.GetString(Convert.FromHexString(m.Groups[2].Value));
                }

                return s =>
                {
                    var hex = s.Trim('<', '>');
                    var result = new StringBuilder();
                    for (var i = 0; i < hex.Length; i += 4)
                    {
                        result.Append(map[int.Parse(hex.Substring(i, 4), NumberStyles.HexNumber)]);
                    }
                    return result.ToString();
                };
            }

            // single byte WinAnsi (code page 1252) literal strings
            return s => WinAnsiDecode(Unescape(s.Substring(1, s.Length - 2)));
        }

        private static byte[] Unescape(string literal)
        {
            var result = new List<byte>();
            for (var i = 0; i < literal.Length; i++)
            {
                if (literal[i] != '\\')
                {
                    result.Add((byte)literal[i]);
                    continue;
                }

                i++;
                if (char.IsDigit(literal[i]))
                {
                    var digits = new string(literal.Skip(i).Take(3).TakeWhile(char.IsDigit).ToArray());
                    result.Add(Convert.ToByte(digits, 8));
                    i += digits.Length - 1;
                }
                else
                {
                    result.Add((byte)literal[i]);
                }
            }
            return result.ToArray();
        }

        private static readonly Dictionary<byte, char> _winAnsiHigh = new Dictionary<byte, char>
        {
            { 128, '€' }, { 130, '‚' }, { 131, 'ƒ' }, { 132, '„' }, { 133, '…' },
            { 134, '†' }, { 135, '‡' }, { 136, 'ˆ' }, { 137, '‰' }, { 138, 'Š' },
            { 139, '‹' }, { 140, 'Œ' }, { 142, 'Ž' }, { 145, '‘' }, { 146, '’' },
            { 147, '“' }, { 148, '”' }, { 149, '•' }, { 150, '–' }, { 151, '—' },
            { 152, '˜' }, { 153, '™' }, { 154, 'š' }, { 155, '›' }, { 156, 'œ' },
            { 158, 'ž' }, { 159, 'Ÿ' }
        };

        private static string WinAnsiDecode(byte[] bytes)
        {
            return new string(bytes.Select(b => _winAnsiHigh.TryGetValue(b, out var c) ? c : (char)b).ToArray());
        }
    }

    public class PdfObjectInfo
    {
        public int Number { get; set; }
        public string Dictionary { get; set; }
        public byte[] Stream { get; set; }
        public string StreamText => Stream == null ? null : Encoding.Latin1.GetString(Stream);
    }
}
