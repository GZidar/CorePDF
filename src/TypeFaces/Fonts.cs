﻿using System;
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
                    CharacterSet = " !\"#$%&'()*+,-./0123456789:;<=>?@ABCDEFGHIJKLMNOPQRSTUVWXYZ[\\]^_{}",
                    StartCharacter = "{",
                    StopCharacter = "}",
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
                        600,600,600,600,600,600
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
                        "11010000100",
                        "11000111010"
                    }
                },
                new Barcode
                {
                    CharacterSet = "1234567890ABCDEFGHIJKLMNOPQRSTUVWXYZ-. *+/$%",
                    StartCharacter = "*",
                    StopCharacter = "*",
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
                        "10110100010110",//"TTt.tt",
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
            // Details for other fonts can be found in Adobe Font Manager (afm) files. They contain all 
            // the details you will need to add another font.
            //
            // Please Note: Any font other than the native PDF fonts will have to be embedded.

            var result = new List<Font>()
            {
                new Font
                {
                    BaseFont = FONTSANSSERIF,
                    FontName = FONTSANSSERIF,
                    Metrics = new List<int> {
                        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,278,278,355,556,556,889,667,
                        222,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,278,278, 584,584,
                        584,556,1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,667,778,722,667,611,
                        722,667,944,667,667,611,278,278,278,469,556,222,556,556,500,556,556,278,556,556,222,222,500,
                        222,833,556,556,556,556,333,500,278,556,500,722,500,500,500,334,260,334,584,333,556,556,167,
                        556,556,556,556,191,333,556,333,333,500,500,556,556,556,278,537,350,222,333,333,556,1000,1000,
                        611,333,333,333,333,333,333,333,333,333,333,333,333,333,1000,1000,370,556,778,1000,365,889,
                        278,222,611,944,611
                    }
                },
                new Font
                {
                    BaseFont = FONTSANSSERIF,
                    FontName = FONTSANSSERIFBOLD,
                    Bold = true,
                    Metrics = new List<int> {
                        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,278,333,474,556,556,889,722,
                        278,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,333,333,584,584,
                        584,611,975,722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,667,778,722,667,611,
                        722,667,944,667,667,611,333,278,333,584,556,278,556,611,556,611,556,333,611,611,278,278,556,
                        278,889,611,611,611,611,389,556,333,611,556,778,556,556,500,389,280,389,584,333,556,556,167,
                        556,556,556,556,238,500,556,333,333,611,611,556,556,556,278,556,350,278,500,500,556,1000,1000,
                        611,333,333,333,333,333,333,333,333,333,333,333,333,333,1000,1000,370,611,778,1000,365,889,
                        278,278,611,944,611
                    }
                },
                new Font
                {
                    BaseFont = FONTSANSSERIF,
                    FontName = FONTSANSSERIFBOLDITALIC,
                    Bold = true,
                    Italic = true,
                    Metrics = new List<int> {
                        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,278,333,474,556,556,889,722,
                        278,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,333,333,584,584,
                        584,611,975,722,722,722,722,667,611,778,722,278,556,722,611,833,722,778,667,778,722,667,611,
                        722,667,944,667,667,611,333,278,333,584,556,278,556,611,556,611,556,333,611,611,278,278,556,
                        278,889,611,611,611,611,389,556,333,611,556,778,556,556,500,389,280,389,584,333,556,556,167,
                        556,556,556,556,238,500,556,333,333,611,611,556,556,556,278,556,350,278,500,500,556,1000,1000,
                        611,333,333,333,333,333,333,333,333,333,333,333,333,333,1000,1000,370,611,778,1000,365,889,
                        278,278,611,944,611
                    }
                },
                new Font
                {
                    BaseFont = FONTSANSSERIF,
                    FontName = FONTSANSSERIFITALIC,
                    Italic = true,
                    Metrics = new List<int> {
                        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,278,278,355,556,556,889,667,
                        222,333,333,389,584,278,333,278,278,556,556,556,556,556,556,556,556,556,556,278,278,584,584,
                        584,556,1015,667,667,722,722,667,611,778,722,278,500,667,556,833,722,778,667,778,722,667,611,
                        722,667,944,667,667,611,278,278,278,469,556,222,556,556,500,556,556,278,556,556,222,222,500,
                        222,833,556,556,556,556,333,500,278,556,500,722,500,500,500,334,260,334,584,333,556,556,167,
                        556,556,556,556,191,333,556,333,333,500,500,556,556,556,278,537,350,222,333,333,556,1000,1000,
                        611,333,333,333,333,333,333,333,333,333,333,333,333,333,1000,1000,370,556,778,1000,365,889,
                        278,222,611,944,611
                    }
                },
                new Font
                {
                    BaseFont = FONTSERIF,
                    FontName = FONTSERIF,
                    Metrics = new List<int> {
                        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,250,333,408,500,500,833,778,
                        333,333,333,500,564,250,333,250,278,500,500,500,500,500,500,500,500,500,500,278,278,564,564,
                        564,444,921,722,667,667,722,611,556,722,722,333,389,722,611,889,722,722,556,722,667,556,611,
                        722,722,944,722,722,611,333,278,333,469,500,333,444,500,444,500,444,333,500,500,278,278,500,
                        278,778,500,500,500,500,333,389,278,500,500,722,500,500,444,480,200,480,541,333,500,500,167,
                        500,500,500,500,180,444,500,333,333,556,556,500,500,500,250,453,350,333,444,444,500,1000,1000,
                        444,333,333,333,333,333,333,333,333,333,333,333,333,333,1000,889,276,611,722,889,310,667,278,
                        278,500,722,500
                    }
                },
                new Font
                {
                    BaseFont = FONTSERIF,
                    FontName = FONTSERIFBOLD,
                    Bold = true,
                    Metrics = new List<int> {
                        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,250,333,555,500,500,1000,833,
                        333,333,333,500,570,250,333,250,278,500,500,500,500,500,500,500,500,500,500,333,333,570,570,
                        570,500,930,722,667,722,722,667,611,778,778,389,500,778,667,944,722,778,611,778,722,556,667,
                        722,722,1000,722,722,667,333,278,333,581,500,333,500,556,444,556,444,333,500,556,278,333,556,
                        278,833,556,500,556,556,444,389,333,556,500,722,500,500,444,394,220,394,520,333,500,500,167,
                        500,500,500,500,278,500,500,333,333,556,556,500,500,500,250,540,350,333,500,500,500,1000,1000,
                        500,333,333,333,333,333,333,333,333,333,333,333,333,333,1000,1000,300,667,778,1000,330,722,278,
                        278,500,722,556
                    }
                },
                new Font
                {
                    BaseFont = FONTSERIF,
                    FontName = FONTSERIFBOLDITALIC,
                    Bold = true,
                    Italic = true,
                    Metrics = new List<int> {
                        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,250,389,555,500,500,833,778,
                        333,333,333,500,570,250,333,250,278,500,500,500,500,500,500,500,500,500,500,333,333,570,570,
                        570,500,832,667,667,667,722,667,667,722,778,389,500,667,611,889,722,722,611,722,667,556,611,
                        722,667,889,667,611,611,333,278,333,570,500,333,500,500,444,500,444,333,500,556,278,278,500,
                        278,778,556,500,500,500,389,389,278,556,444,667,500,444,389,348,220,348,570,389,500,500,167,
                        500,500,500,500,278,500,500,333,333,556,556,500,500,500,250,500,350,333,500,500,500,1000,1000,
                        500,333,333,333,333,333,333,333,333,333,333,333,333,333,1000,944,266,611,722,944,300,722,278,
                        278,500,722,500
                    }
                },
                new Font
                {
                    BaseFont = FONTSERIF,
                    FontName = FONTSERIFITALIC,
                    Italic = true,
                    Metrics = new List<int> {
                        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,250,333,420,500,500,833,778,
                        333,333,333,500,675,250,333,250,278,500,500,500,500,500,500,500,500,500,500,333,333,675,675,
                        675,500,920,611,611,667,722,611,611,722,722,333,444,667,556,833,667,722,611,722,611,500,556,
                        722,611,833,611,556,556,389,278,389,422,500,333,500,500,444,500,444,278,500,500,278,278,444,
                        278,722,500,500,500,500,389,389,278,500,444,667,444,444,389,400,275,400,541,389,500,500,167,
                        500,500,500,500,214,556,500,333,333,500,500,500,500,500,250,523,350,333,556,556,500,889,1000,
                        500,333,333,333,333,333,333,333,333,333,333,333,333,333,889,889,276,556,722,944,310,667,278,
                        278,500,667,500
                    }
                },
                new Font
                {
                    BaseFont = FONTFIXED,
                    FontName = FONTFIXED,
                    Metrics = new List<int> {
                        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600
                    }
                },
                new Font
                {
                    BaseFont = FONTFIXED,
                    FontName = FONTFIXEDBOLD,
                    Bold = true,
                    Metrics = new List<int> {
                        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600
                    }
                },
                new Font
                {
                    BaseFont = FONTFIXED,
                    FontName = FONTFIXEDBOLDITALIC,
                    Bold = true,
                    Italic = true,
                    Metrics = new List<int> {
                        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600
                    }
                },
                new Font
                {
                    BaseFont = FONTFIXED,
                    FontName = FONTFIXEDITALIC,
                    Italic = true,
                    Metrics = new List<int> {
                        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,600,
                        600,600,600,600
                    }
                },
                new Font
                {
                    BaseFont = FONTSYMBOL,
                    FontName = FONTSYMBOL,
                    Metrics = new List<int> {
                        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,250,333,713,500,549,833,778,
                        439,333,333,500,549,250,549,250,278,500,500,500,500,500,500,500,500,500,500,278,278,549,549,
                        549,444,549,722,667,722,612,611,763,603,722,333,631,722,686,889,722,722,768,741,556,592,611,
                        690,439,768,645,795,611,333,863,333,658,500,500,631,549,549,494,439,521,411,603,329,603,549,
                        549,576,521,549,549,521,549,603,439,576,713,686,493,686,494,480,200,480,549,750,620,247,549,
                        167,713,500,753,753,753,753,1042,987,603,987,603,400,549,411,549,549,713,494,460,549,549,549,
                        549,1000,603,1000,658,823,686,795,987,768,768,823,768,768,713,713,713,713,713,713,713,768,713,
                        790,790,890,823,549,250,713,603,603,1042,987,603,987,603,494,329,790,790,786,713,384,384,384,
                        384,384,384,494,494,494,494,329,274,686,686,686,384,384,384,384,384,384,494,494,494
                    }
                },
                new Font
                {
                    BaseFont = FONTZAPF,
                    FontName = FONTZAPF,
                    Metrics = new List<int> {
                        0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,0,278,974,961,974,980,719,789,
                        790,791,690,960,939,549,855,911,933,911,945,974,755,846,762,761,571,677,763,760,759,754,494,
                        552,537,577,692,786,788,788,790,793,794,816,823,789,841,823,833,816,831,923,744,723,749,790,
                        792,695,776,768,792,759,707,708,682,701,826,815,789,789,707,687,696,689,786,787,713,791,785,
                        791,873,761,762,762,759,759,892,892,788,784,438,138,277,415,392,392,668,668,390,390,317,317,
                        276,276,509,509,410,410,234,234,334,334,732,544,544,910,667,760,760,776,595,694,626,788,788,
                        788,788,788,788,788,788,788,788,788,788,788,788,788,788,788,788,788,788,788,788,788,788,788,
                        788,788,788,788,788,788,788,788,788,788,788,788,788,788,788,894,838,1016,458,748,924,748,918,
                        927,928,928,834,873,828,924,924,917,930,931,463,883,836,836,867,867,696,696,874,874,760,946,
                        771,865,771,888,967,888,831,873,927,970,918
                    }
                }
            };

            return result;
        }

        public static Font Font(string name, bool bold = false, bool italic = false)
        {
            return Styles().FirstOrDefault(f => f.BaseFont.ToLower() == name.ToLower() && f.Bold == bold && f.Italic == italic);
        }

        public static Barcode Font(string name)
        {
            return Barcodes().FirstOrDefault(f => f.BaseFont.ToLower() == name.ToLower());
        }
    }

}
