using System;
using System.Collections.Generic;
using System.Text;

namespace CorePDF.Writing
{
    /// <summary>
    /// Helpers for writing PDF string and name objects. Everything produced here is plain ASCII
    /// so it can be safely written through the document's text writer.
    /// </summary>
    internal static class PdfString
    {
        /// <summary>
        /// Writes bytes as a literal string, escaping the delimiters and using octal escapes
        /// for anything outside printable ASCII. eg: (Caf\351)
        /// </summary>
        public static string Literal(IEnumerable<byte> bytes)
        {
            var result = new StringBuilder("(");
            foreach (var b in bytes)
            {
                switch (b)
                {
                    case (byte)'(':
                    case (byte)')':
                    case (byte)'\\':
                        result.Append('\\').Append((char)b);
                        break;
                    default:
                        if (b < 0x20 || b > 0x7E)
                        {
                            result.Append('\\').Append(Convert.ToString(b, 8).PadLeft(3, '0'));
                        }
                        else
                        {
                            result.Append((char)b);
                        }
                        break;
                }
            }
            return result.Append(')').ToString();
        }

        /// <summary>
        /// Writes a "text string" (used for document information, bookmarks, etc). Strings that
        /// are entirely printable ASCII are written as literals, anything else is written as
        /// UTF-16BE with a byte order mark, as required by the PDF specification.
        /// </summary>
        public static string Text(string value)
        {
            value = value ?? "";

            var isAscii = true;
            foreach (var c in value)
            {
                if (c < 0x20 || c > 0x7E)
                {
                    isAscii = false;
                    break;
                }
            }

            if (isAscii)
            {
                return Literal(Encoding.ASCII.GetBytes(value));
            }

            var result = new StringBuilder("<FEFF");
            foreach (var b in Encoding.BigEndianUnicode.GetBytes(value))
            {
                result.Append(b.ToString("X2"));
            }
            return result.Append('>').ToString();
        }

        /// <summary>
        /// Writes a URI for a link action. URIs must be 7-bit ASCII, so any other characters
        /// are percent-encoded as UTF-8.
        /// </summary>
        public static string Uri(string value)
        {
            var bytes = new List<byte>();
            foreach (var b in Encoding.UTF8.GetBytes(value ?? ""))
            {
                if (b < 0x21 || b > 0x7E)
                {
                    bytes.AddRange(Encoding.ASCII.GetBytes("%" + b.ToString("X2")));
                }
                else
                {
                    bytes.Add(b);
                }
            }
            return Literal(bytes);
        }

        /// <summary>
        /// Writes a PDF name (without the leading slash), escaping any characters that are
        /// not allowed to appear in a name.
        /// </summary>
        public static string Name(string value)
        {
            var result = new StringBuilder();
            foreach (var b in Encoding.UTF8.GetBytes(value ?? ""))
            {
                if (b < 0x21 || b > 0x7E || "#()<>[]{}/%".IndexOf((char)b) >= 0)
                {
                    result.Append('#').Append(b.ToString("X2"));
                }
                else
                {
                    result.Append((char)b);
                }
            }
            return result.ToString();
        }

        /// <summary>
        /// Writes bytes as a hexadecimal string. eg: &lt;00410042&gt;
        /// </summary>
        public static string Hex(byte[] bytes)
        {
            return "<" + Convert.ToHexString(bytes) + ">";
        }
    }
}
