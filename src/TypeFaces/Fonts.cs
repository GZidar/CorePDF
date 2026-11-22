using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace CorePDF.TypeFaces
{
    /// <summary>
    /// Holds the list of fonts that are available for use in the document.
    /// </summary>
    public static class Fonts
    {
        /// <summary>
        /// These 14 fonts are guaranteed to be supported by all PDF viewers without the need 
        /// for embedding. As such these are the only fonts supported by the PDF generator.
        /// </summary>
        public const string FONTSANSSERIF = "Helvetica";
        public const string FONTSANSSERIFBOLD = "Helvetica-Bold";
        public const string FONTSANSSERIFBOLDITALIC = "Helvetica-BoldOblique";
        public const string FONTSANSSERIFITALIC = "Helvetica-Oblique";

        public const string FONTSERIF = "Times-Roman";
        public const string FONTSERIFBOLD = "Times-Bold";
        public const string FONTSERIFBOLDITALIC = "Times-BoldItalic";
        public const string FONTSERIFITALIC = "Times-Italic";

        public const string FONTFIXED = "Courier";
        public const string FONTFIXEDBOLD = "Courier-Bold";
        public const string FONTFIXEDBOLDITALIC = "Courier-BoldOblique";
        public const string FONTFIXEDITALIC = "Courier-Oblique";

        public const string FONTSYMBOL = "Symbol";
        public const string FONTZAPF = "ZapfDingbats";

        public const string BARCODE2OF5 = "Code2of5";
        public const string BARCODE39 = "Code39";
        public const string BARCODE128 = "Code128";

        public static List<Barcode> Barcodes()
        {
            var result = new List<Barcode>
            {
                new Barcode
                {
                    // values 0-94 are the printable characters, 95-102 (special codes that only appear as the
                    // check digit) are characters 127-134, then the start (newline) and stop (carriage return) characters
                    CharacterSet = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_`abcdefghijklmnopqrstuvwxyz{|}~\u007F\u0080\u0081\u0082\u0083\u0084\u0085\u0086\n\r",
                    PreProcessor = (string input) =>
                    {
                        // code 128 has a check digit so this needs to be calculated prior to forming the barcode.
                        // the check digit is appended after the input text prior to the stop character but it's
                        // calculation includes the start character.
                        var checktotal = 104; // this is the value of the code 128 B

                        for (var i = 0; i < input.Length; i++)
                        {
                            var letter = Convert.ToChar(input[i]);
                            var codeValue = letter - 32; // offset by the non-printable characters

                            checktotal += (codeValue * (i+1));
                        }

                        var checkdigit = (checktotal % 103) + 32; // 103 is the value that all code 128 codes use

                        return string.Format("{0}{1}{2}{3}", "\n", input, (char)checkdigit, "\r");
                    },
                    FontName = BARCODE128,
                    BaseFont = BARCODE128,
                    Metrics = new List<int>
                    {
                        600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,650
                    },
                    Definitions = new List<string>
                    {
                        "11011001100",
                        "11001101100",
                        "11001100110",
                        "10010011000",
                        "10010001100",
                        "10001001100",
                        "10011001000",
                        "10011000100",
                        "10001100100",
                        "11001001000",
                        "11001000100",
                        "11000100100",
                        "10110011100",
                        "10011011100",
                        "10011001110",
                        "10111001100",
                        "10011101100",
                        "10011100110",
                        "11001110010",
                        "11001011100",
                        "11001001110",
                        "11011100100",
                        "11001110100",
                        "11101101110",
                        "11101001100",
                        "11100101100",
                        "11100100110",
                        "11101100100",
                        "11100110100",
                        "11100110010",
                        "11011011000",
                        "11011000110",
                        "11000110110",
                        "10100011000",
                        "10001011000",
                        "10001000110",
                        "10110001000",
                        "10001101000",
                        "10001100010",
                        "11010001000",
                        "11000101000",
                        "11000100010",
                        "10110111000",
                        "10110001110",
                        "10001101110",
                        "10111011000",
                        "10111000110",
                        "10001110110",
                        "11101110110",
                        "11010001110",
                        "11000101110",
                        "11011101000",
                        "11011100010",
                        "11011101110",
                        "11101011000",
                        "11101000110",
                        "11100010110",
                        "11101101000",
                        "11101100010",
                        "11100011010",
                        "11101111010",
                        "11001000010",
                        "11110001010",
                        "10100110000",
                        "10100001100",
                        "10010110000",
                        "10010000110",
                        "10000101100",
                        "10000100110",
                        "10110010000",
                        "10110000100",
                        "10011010000",
                        "10011000010",
                        "10000110100",
                        "10000110010",
                        "11000010010",
                        "11001010000",
                        "11110111010",
                        "11000010100",
                        "10001111010",
                        "10100111100",
                        "10010111100",
                        "10010011110",
                        "10111100100",
                        "10011110100",
                        "10011110010",
                        "11110100100",
                        "11110010100",
                        "11110010010",
                        "11011011110",
                        "11011110110",
                        "11110110110",
                        "10101111000",
                        "10100011110",
                        "10001011110",
                        "10111101000",
                        "10111100010",
                        "11110101000",
                        "11110100010",
                        "10111011110",
                        "10111101110",
                        "11101011110",
                        "11110101110",
                        "11010010000",
                        "1100011101011"
                    }
                },
                new Barcode
                {
                    CharacterSet = "1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ-. *+/$%",
                    PreProcessor = (string input) =>
                    {
                        // only uppercase characters with an asterisk as the start and stop character
                        return string.Format("{0}{1}{0}", "*", input.ToUpper());
                    },
                    FontName = BARCODE39,
                    BaseFont = BARCODE39,
                    Metrics = new List<int>
                    {
                        700,700,700,700,700,700,700,700,700,700,
                        700,700,700,700,700,700,700,700,700,700,
                        700,700,700,700,700,700,700,700,700,700,
                        700,700,700,700,700,700,700,700,700,700,
                        700,700,700
                    },
                    Definitions = new List<string>
                    {
                        "11010001010110", //"Tt.ttT",
                        "10110001010110", //"tT.ttT",
                        "11011000101010", //"TT.ttt",
                        "10100011010110", //"tt.TtT",
                        "11010001101010", //"Tt.Ttt",
                        "10110001101010", //"tT.Ttt",
                        "10100010110110", //"tt.tTT",
                        "11010001011010", //"Tt.tTt",
                        "10110001011010",//"tT.tTt",
                        "10100011011010",//"tt.TTt",
                        "11010100010110",//"Ttt.tT",
                        "10110100010110",//"tTt.tT",
                        "11011010001010",//"TTt.tt",
                        "10101100010110",//"ttT.tT",
                        "11010110001010",//"TtT.tt",
                        "10110110001010",//"tTT.tt",
                        "10101000110110",//"ttt.TT",
                        "11010100011010",//"Ttt.Tt",
                        "10110100011010",//"tTt.Tt",
                        "10101100011010",//"ttT.Tt",
                        "11010101000110",//"Tttt.T",
                        "10110101000110",//"tTtt.T",
                        "11011010100010",//"TTtt.t",
                        "10101101000110",//"ttTt.T",
                        "11010110100010",//"TtTt.t",
                        "10110110100010",//"tTTt.t",
                        "10101011000110",//"tttT.T",
                        "11010101100010",//"TttT.t",
                        "10110101100010",//"tTtT.t",
                        "10101101100010",//"ttTT.t",
                        "11000101010110",//"T.tttT",
                        "10001101010110",//"t.TttT",
                        "11000110101010",//"T.Tttt",
                        "10001011010110",//"t.tTtT",
                        "11000101101010",//"T.tTtt",
                        "10001101101010",//"t.TTtt",
                        "10001010110110",//"t.ttTT",
                        "11000101011010",//"T.ttTt",
                        "10001101011010",//"t.TtTt",
                        "10001011011010",//"t.tTTt",
                        "10010100100100",//"t.tt.t.t",
                        "10010010100100",//"t.t.tt.t",
                        "10010010010100",//"t.t.t.tt",
                        "10100100100100",//"tt.t.t.t"
                    }
                }
            };

            return result;
        }

        /// <summary>
        /// Enumerate the fonts that have been defined as being available for use.
        /// </summary>
        /// <returns></returns>
        public static List<Font> Styles()
        {
            // The widths of the standard fonts come from Adobe's font metric (AFM) files and are
            // indexed by the character's code in the font's encoding (see StandardFontMetrics).
            //
            // Please Note: Any font other than the native PDF fonts will have to be embedded.
            return new List<Font>
            {
                StandardFont(FONTSANSSERIF, FONTSANSSERIF, false, false, StandardFontMetrics.Helvetica),
                StandardFont(FONTSANSSERIF, FONTSANSSERIFBOLD, true, false, StandardFontMetrics.HelveticaBold),
                StandardFont(FONTSANSSERIF, FONTSANSSERIFBOLDITALIC, true, true, StandardFontMetrics.HelveticaBoldOblique),
                StandardFont(FONTSANSSERIF, FONTSANSSERIFITALIC, false, true, StandardFontMetrics.HelveticaOblique),
                StandardFont(FONTSERIF, FONTSERIF, false, false, StandardFontMetrics.TimesRoman),
                StandardFont(FONTSERIF, FONTSERIFBOLD, true, false, StandardFontMetrics.TimesBold),
                StandardFont(FONTSERIF, FONTSERIFBOLDITALIC, true, true, StandardFontMetrics.TimesBoldItalic),
                StandardFont(FONTSERIF, FONTSERIFITALIC, false, true, StandardFontMetrics.TimesItalic),
                StandardFont(FONTFIXED, FONTFIXED, false, false, StandardFontMetrics.Courier),
                StandardFont(FONTFIXED, FONTFIXEDBOLD, true, false, StandardFontMetrics.CourierBold),
                StandardFont(FONTFIXED, FONTFIXEDBOLDITALIC, true, true, StandardFontMetrics.CourierBoldOblique),
                StandardFont(FONTFIXED, FONTFIXEDITALIC, false, true, StandardFontMetrics.CourierOblique),
                // the symbolic fonts use their own built-in encoding rather than WinAnsi
                new Font { BaseFont = FONTSYMBOL, FontName = FONTSYMBOL, Encoding = null, Metrics = new List<int>(StandardFontMetrics.Symbol) },
                new Font { BaseFont = FONTZAPF, FontName = FONTZAPF, Encoding = null, Metrics = new List<int>(StandardFontMetrics.ZapfDingbats) }
            };
        }

        private static Font StandardFont(string baseFont, string fontName, bool bold, bool italic, int[] metrics)
        {
            return new Font
            {
                BaseFont = baseFont,
                FontName = fontName,
                Bold = bold,
                Italic = italic,
                Metrics = new List<int>(metrics)
            };
        }

        public static Font Font(string name, bool bold = false, bool italic = false, List<Font> fonts = null)
        {
            if (fonts == null)
            {
                return Styles().FirstOrDefault(f => f.BaseFont.ToLower() == name.ToLower() && f.Bold == bold && f.Italic == italic);
            }

            return fonts.FirstOrDefault(f => f.BaseFont.ToLower() == name.ToLower() && f.Bold == bold && f.Italic == italic);
        }

        public static Barcode Font(string name)
        {
            return Barcodes().FirstOrDefault(f => f.BaseFont.ToLower() == name.ToLower());
        }
    }

}
