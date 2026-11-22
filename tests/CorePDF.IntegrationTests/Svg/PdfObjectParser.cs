using System;
using System.Collections.Generic;
using System.Globalization;

namespace CorePDF.IntegrationTests.Svg
{
    /// <summary>
    /// A PDF name, eg: /DeviceRGB
    /// </summary>
    internal sealed record PdfName(string Value);

    /// <summary>
    /// An indirect object reference, eg: 12 0 R
    /// </summary>
    internal sealed record PdfReference(int Number);

    /// <summary>
    /// Parses PDF objects written as text: dictionaries, arrays, names, numbers, strings,
    /// booleans and references. Dictionaries become Dictionary&lt;string, object&gt; (keys
    /// without the slash) and arrays become List&lt;object&gt;.
    /// </summary>
    internal static class PdfObjectParser
    {
        public static object Parse(string text)
        {
            var pos = 0;
            return ParseValue(text, ref pos);
        }

        private static object ParseValue(string text, ref int pos)
        {
            SkipWhitespace(text, ref pos);
            var c = text[pos];

            if (c == '<' && pos + 1 < text.Length && text[pos + 1] == '<')
            {
                pos += 2;
                var dictionary = new Dictionary<string, object>();
                while (true)
                {
                    SkipWhitespace(text, ref pos);
                    if (text[pos] == '>' && text[pos + 1] == '>')
                    {
                        pos += 2;
                        return dictionary;
                    }
                    var key = (PdfName)ParseValue(text, ref pos);
                    dictionary[key.Value] = ParseValue(text, ref pos);
                }
            }

            if (c == '[')
            {
                pos++;
                var list = new List<object>();
                while (true)
                {
                    SkipWhitespace(text, ref pos);
                    if (text[pos] == ']')
                    {
                        pos++;
                        return list;
                    }
                    list.Add(ParseValue(text, ref pos));
                }
            }

            if (c == '/')
            {
                var start = ++pos;
                while (pos < text.Length && !IsDelimiter(text[pos])) pos++;
                return new PdfName(text.Substring(start, pos - start));
            }

            if (c == '(')
            {
                var start = pos;
                var depth = 0;
                do
                {
                    if (text[pos] == '\\') pos++;
                    else if (text[pos] == '(') depth++;
                    else if (text[pos] == ')') depth--;
                    pos++;
                } while (depth > 0);
                return text.Substring(start, pos - start);
            }

            if (c == '<')
            {
                var start = pos;
                pos = text.IndexOf('>', pos) + 1;
                return text.Substring(start, pos - start);
            }

            if (char.IsDigit(c) || c == '-' || c == '+' || c == '.')
            {
                var start = pos++;
                while (pos < text.Length && (char.IsDigit(text[pos]) || text[pos] == '.')) pos++;
                var number = double.Parse(text.Substring(start, pos - start), CultureInfo.InvariantCulture);

                // a reference looks like "12 0 R"
                var look = pos;
                SkipWhitespace(text, ref look);
                var genStart = look;
                while (look < text.Length && char.IsDigit(text[look])) look++;
                if (look > genStart)
                {
                    SkipWhitespace(text, ref look);
                    if (look < text.Length && text[look] == 'R' && (look + 1 == text.Length || IsDelimiter(text[look + 1])))
                    {
                        pos = look + 1;
                        return new PdfReference((int)number);
                    }
                }
                return number;
            }

            var keywordStart = pos;
            while (pos < text.Length && !IsDelimiter(text[pos])) pos++;
            var keyword = text.Substring(keywordStart, pos - keywordStart);
            return keyword switch
            {
                "true" => true,
                "false" => false,
                "null" => null,
                _ => throw new FormatException($"Unexpected '{keyword}' at {keywordStart}")
            };
        }

        private static void SkipWhitespace(string text, ref int pos)
        {
            while (pos < text.Length)
            {
                if (char.IsWhiteSpace(text[pos])) pos++;
                else if (text[pos] == '%') { while (pos < text.Length && text[pos] != '\n') pos++; }
                else break;
            }
        }

        private static bool IsDelimiter(char c)
        {
            return char.IsWhiteSpace(c) || "/[]<>()%".IndexOf(c) >= 0;
        }
    }
}
