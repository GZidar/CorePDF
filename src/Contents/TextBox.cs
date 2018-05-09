using CorePDF.Pages;
using CorePDF.TypeFaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CorePDF.Contents
{
    /// <summary>
    /// Defines any text content to be included in the PDF page
    /// </summary>
    public class TextBox : Content
    {
        /// <summary>
        /// The text to be added to the document
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// Determines if the text is aligned to the left, right, or centre of the 
        /// text box
        /// </summary>
        public Alignment TextAlignment { get; set; }

        /// <summary>
        /// Controls how the PosX and PosY co-ordinates are used to position the
        /// content. If set to "Bottom" then the PosX and PosY will indicate the
        /// position of the bottom line of the text block. If set to "Top" then 
        /// PosX and PosY will indicate the position of the top line of the text block.
        /// </summary>
        public PositionAnchor Position { get; set; } = PositionAnchor.Top;

        /// <summary>
        /// Specifies the font that will be used to show the text
        /// </summary>
        public string FontFace { get; set; } = Fonts.FONTSANSSERIF;

        /// <summary>
        /// The font size in points
        /// </summary>
        public int FontSize { get; set; } = 10;

        /// <summary>
        /// The spacing between each line of text. Will default to the fontsize if
        /// left as zero.
        /// </summary>
        public int LineHeight {
            get
            {
                if (_lineHeight == 0)
                {
                    return FontSize;
                }
                return _lineHeight;
            }
            set
            {
                _lineHeight = value;
            }
        }
        private int _lineHeight;

        /// <summary>
        /// Specifies the color of the text. This is specified using HTML hexadecimal 
        /// color syntax and must be 6 characters long. eg:<br /> 
        ///     #ffffff = white, <br />
        ///     #ff0000 is red, <br />
        ///     #000000 = black.<br />
        /// Will default to black if left blank.
        /// </summary>
        public string Color { get; set; }

        public override void PrepareStream(PageRoot pageRoot, Size pageSize, List<Font> fonts, bool compress)
        {
            // don't do anything of there is no text
            if (string.IsNullOrEmpty(Text)) return;

            if (PosY < 0 || PosY > pageSize.ContentHeight)
            {
                throw new ArgumentOutOfRangeException("PosY", "TextBox vertical position (PosY) is outside the bounds of the page.");
            }

            var result = "";
            var contentFont = fonts.Find(f => f.FontName == FontFace);

            var textHeight = Height;
            var textWidth = Width;
            if (textWidth == 0)
            {
                // the text hasn't been constrained to a specific width so calculate that constraint
                // based on alignment and position. 
                switch (TextAlignment)
                {
                    case Alignment.Center:
                        // the width is the lesser value of the current X position and the
                        // distance to either the left or right page content limits doubled
                        textWidth = ((pageSize.ContentWidth - PosX) < PosX ? (pageSize.ContentWidth - PosX) : PosX) * 2;
                        break;
                    case Alignment.Right:
                        // the width is the current X Position;
                        textWidth = (PosX);
                        break;
                    case Alignment.Left:
                        // the width is the difference between the current X Position and the width of the page;
                        textWidth = (pageSize.ContentWidth - PosX);
                        break;
                }
            }

            if (textWidth < 0)
            {
                // invalid X coordinates for item
                throw new ArgumentOutOfRangeException("PosX", "TextBox horizontal position (PosX) is outside the bounds of the page.");
            }

            // now split the strings at their line breaks and process each paragraph so that it fits within the width
            var textLines = Text.Split('\n');
            var adjustedText = "";
            var calcHeight = 0;
            var isbold = false;
            var isitalic = false;
            for (var l = 0; l < textLines.Length; l++)
            {
                // split each line into words
                var lineWords = textLines[l].Split(' ');
                var width = 0;
                for (var num = 0; num < lineWords.Length; num++)
                {
                    if (num + 1 < lineWords.Length)
                    {
                        lineWords[num] += " ";
                    }

                    var style = "";
                    var word = lineWords[num].ToLower();
                    if (word.Contains("<b>"))
                    {
                        isbold = true;
                        if (isitalic)
                        {
                            style = "{{BI}}";
                        }
                        else
                        {
                            style = "{{B}}";
                        }
                    }
                    if (word.Contains("<i>"))
                    {
                        isitalic = true;
                        if (isbold)
                        {
                            style = "{{BI}}";
                        }
                        else
                        {
                            style = "{{I}}";
                        }
                    }

                    var pureWord = lineWords[num].Replace("<b>", "");
                    pureWord = pureWord.Replace("<B>", "");
                    pureWord = pureWord.Replace("<i>", "");
                    pureWord = pureWord.Replace("<I>", "");
                    pureWord = pureWord.Replace("</b>", "");
                    pureWord = pureWord.Replace("</B>", "");
                    pureWord = pureWord.Replace("</i>", "");
                    pureWord = pureWord.Replace("</I>", "");


                    // if the word was only the bold or italic tag then loop to the next word
                    if (!string.IsNullOrEmpty(lineWords[num]))
                    {
                        // determine the font that should be used to calculate the width
                        var font = fonts.Find(f => f.BaseFont == contentFont.BaseFont && f.Bold == isbold && f.Italic == isitalic);

                        var wordWidth = StringLength(pureWord, FontSize, font);
                        if (width + wordWidth > textWidth)
                        {
                            // this word will take us over the edge so get rid of the trailing space and add a linebreak
                            adjustedText = adjustedText.Substring(0, adjustedText.Length - 1);
                            adjustedText += "\n";
                            calcHeight += LineHeight;
                            width = 0;
                        }

                        if (textHeight == 0 || calcHeight < textHeight)
                        {
                            // this text is still within the box height it's ok to keep adding words
                            adjustedText += (style + pureWord);
                            style = "";
                            width += wordWidth;
                        }
                    }

                    if (word.Contains("</b>"))
                    {
                        if (isitalic)
                        {
                            style = "{{/BI}}{{I}}";
                        }
                        else
                        {
                            style = "{{/B}}";
                        }
                        isbold = false;
                    }
                    if (word.Contains("</i>"))
                    {
                        if (isbold)
                        {
                            style = "{{/BI}}{{B}}";
                        }
                        else
                        {
                            style = "{{/I}}";
                        }
                        isitalic = false;
                    }

                    if (!string.IsNullOrEmpty(style))
                    {
                        adjustedText += style;
                    }
                }

                if (l + 1 < textLines.Length)
                {
                    // Add the newline character back since this is where it used to be
                    adjustedText += "\n";
                }
                calcHeight += LineHeight;
            }

            result = "BT\n";
            result += string.Format("/{0} {1} Tf\n", contentFont.Id, FontSize);

            result += string.Format("{0} TL\n", LineHeight);

            if (!string.IsNullOrEmpty(Color))
            {
                result += string.Format("{0} rg\n", ToPDFColor(Color));
            }
            else
            {
                result += "0 0 0 rg\n";
            }

            textLines = adjustedText.Split('\n');
            var count = 0;
            var curX = PosX;
            var curY = PosY;

            if (Position == PositionAnchor.Bottom)
            {
                // if the string was split across multiple lines then the Y position needs adjusting
                curY += (calcHeight - LineHeight); 
            }

            var stringLength = 0;
            foreach (var line in textLines)
            {
                var prevLength = stringLength;

                var pureLine = line.Replace("{{B}}", "");
                pureLine = pureLine.Replace("{{I}}", "");
                pureLine = pureLine.Replace("{{BI}}", "");
                pureLine = pureLine.Replace("{{/B}}", "");
                pureLine = pureLine.Replace("{{/I}}", "");
                pureLine = pureLine.Replace("{{/BI}}", "");

                // TODO: apply the formatting to properly determine the length of the line.
                stringLength = StringLength(pureLine, FontSize, contentFont);

                count++;
                if (count > 1)
                {
                    curY = 0 - LineHeight;
                }

                switch (TextAlignment)
                {
                    case Alignment.Center:
                        if (count == 1)
                        {
                            curX = PosX - (stringLength / 2);
                        }
                        else
                        {
                            curX = (prevLength - stringLength) / 2;
                        }

                        break;
                    case Alignment.Right:
                        if (count == 1)
                        {
                            curX = (PosX - stringLength);
                        }
                        else
                        {
                            curX = (prevLength - stringLength);
                        }

                        break;
                    case Alignment.Left:
                        if (count > 1)
                        {
                            curX = 0;
                        }

                        break;
                }

                result += string.Format("{0} {1} Td\n", curX, curY);

                // now we are ready to output the line we need to replace the PDF special characters with the their escaped equivalents
                //'(', '/' and ')' are escape characters in adobe so remove them - page 30 of adobe doco. 

                var output = line.Replace("\\", "\\\\");
                output = output.Replace("(", "\\(");
                output = output.Replace(")", "\\)");

                // also need to replace any {{bold/italic}} tags with their PDF equivalents

                output = output.Replace("{{B}}", string.Format(") Tj\n/{0} {1} Tf\n(", fonts.Find(f => f.BaseFont == contentFont.BaseFont && f.Bold && !f.Italic)?.Id ?? contentFont.Id, FontSize));
                output = output.Replace("{{I}}", string.Format(") Tj\n/{0} {1} Tf\n(", fonts.Find(f => f.BaseFont == contentFont.BaseFont && !f.Bold && f.Italic)?.Id ?? contentFont.Id, FontSize));
                output = output.Replace("{{BI}}", string.Format(") Tj\n/{0} {1} Tf\n(", fonts.Find(f => f.BaseFont == contentFont.BaseFont && f.Bold && f.Italic)?.Id ?? contentFont.Id, FontSize));
                output = output.Replace("{{/B}}", string.Format(") Tj\n/{0} {1} Tf\n(", contentFont.Id, FontSize));
                output = output.Replace("{{/I}}", string.Format(") Tj\n/{0} {1} Tf\n(", contentFont.Id, FontSize));
                output = output.Replace("{{/BI}}", string.Format(") Tj\n/{0} {1} Tf\n(", contentFont.Id, FontSize));

                result += string.Format("({0}) Tj\n", output);
            }

            result += "ET";

            if (Height == 0)
            {
                // record the final height of the textbox after it has been rendered
                Height = calcHeight;
            }

            _encodedData = Encoding.UTF8.GetBytes(result);

            base.PrepareStream(compress);
        }

        /// <summary>
        /// Calculates the width of a string in points
        /// </summary>
        /// <param name="text"></param>
        /// <param name="fontSize"></param>
        /// <param name="font"></param>
        /// <returns></returns>
        protected int StringLength(string text, int fontSize, Font font)
        {
            char[] cArray = text.ToCharArray();
            int width = 0;
            foreach (char c in cArray)
            {
                width += font.Metrics[c];
            }
            return (width * fontSize / 1000);
        }
    }
}
