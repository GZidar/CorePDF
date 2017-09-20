using CorePDF.Pages;
using CorePDF.TypeFaces;
using System;
using System.Collections.Generic;
using System.Text;

namespace CorePDF.Contents
{
    public class Barcode : TextBox
    {
        public bool ShowText { get; set; } = false;
        public string TextFont { get; set; } = Fonts.FONTSANSSERIF;
        public string TextColor { get; set; }

        public Barcode()
        {
            // Default to using a barcode font
            FontFace = Fonts.BARCODE39;
        }

        public override void PrepareStream(PageRoot pageRoot, Size pageSize, List<Font> fonts, bool compress)
        {
            // don't do anything of there is no text
            if (string.IsNullOrEmpty(Text)) return;

            var result = "";
            var contentFont = fonts.Find(f => f.FontName == FontFace) as TypeFaces.Barcode;

            if (ShowText && LineHeight < FontSize)
            {
                // increase the height of the barcode area to allow for the text to be shown
                LineHeight += FontSize;
            }

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

            decimal curX = PosX;
                // Add the start/stop characters
            var content = string.Format("{0}{1}{0}", contentFont.StartStopCharacter, Text);
            var stringLength = StringLength(content, FontSize, contentFont);

            switch (TextAlignment)
            {
                case Alignment.Center:
                    curX = PosX - (stringLength / 2);

                    break;
                case Alignment.Right:
                    curX = (PosX - stringLength);

                    break;
                case Alignment.Left:
                    break;
            }

            if (!string.IsNullOrEmpty(Color))
            {
                result += string.Format("{0} RG\n", ToPDFColor(Color));
            }
            else
            {
                result += "0 0 0 RG\n";
            }

            // reset the dash pattern
            result += "[] 0 d\n";
            var strokeWidth = 1;
            result += string.Format("{0} w\n", 1);

            var textAreaStart = 0m;
            var textAreaWidth = 0m;

            char[] cArray = content.ToUpper().ToCharArray();
            foreach (char c in cArray)
            {
                var curY = PosY;
                var lineHeight = LineHeight;
                if (ShowText)
                {
                    if (c == contentFont.StartStopCharacter)
                    {
                        if (textAreaStart != 0)
                        {
                            // at the stop character so figure out how much room is available for the text
                            textAreaWidth = curX - textAreaStart;
                        }
                    }
                    else
                    {
                        curY = PosY + FontSize;
                        lineHeight = lineHeight - FontSize;
                    }
                }

                var width = contentFont.Metrics[contentFont.CharacterSet.IndexOf(c)] * FontSize / 1000;
                var pattern = contentFont.Definitions[contentFont.CharacterSet.IndexOf(c)];

                var pArray = pattern.ToCharArray();
                foreach (char p in pArray)
                {
                    var elementWidth = 0m;
                    var lineWidth = 0m;

                    switch (p)
                    {
                        case 'T':
                            // think line
                            lineWidth = 2m;
                            elementWidth = 3;
                            break;
                        case 't':
                            // thin line
                            lineWidth = 1m;
                            elementWidth = 2m;
                            break;
                        case '.':
                            // this is a space.
                            lineWidth = 0m;
                            elementWidth = 2m;
                            break;
                    }

                    var posX = curX;
                    for (var i = 0; i < lineWidth; i+= strokeWidth)
                    {
                        posX += i;
                        result += string.Format("{0} {1} m\n", posX, curY);
                        result += string.Format("{0} {1} l\n", posX, curY + lineHeight);
                    }

                    curX += elementWidth;
                }

                curX += strokeWidth;

                if (ShowText && c == contentFont.StartStopCharacter)
                {
                    // Save the start position for the text value
                    if (textAreaStart == 0)
                    {
                        textAreaStart = curX;
                    }
                }
            }

            result += "S";

            if (ShowText)
            {
                var textFont = fonts.Find(f => f.FontName == TextFont);
                stringLength = base.StringLength(Text, FontSize, textFont);
                curX = textAreaStart + ((textAreaWidth - stringLength) / 2);

                result += "\nBT\n";
                result += string.Format("/{0} {1} Tf\n", textFont.Id, FontSize);

                if (!string.IsNullOrEmpty(TextColor))
                {
                    result += string.Format("{0} rg\n", ToPDFColor(TextColor));
                }
                else
                {
                    result += "0 0 0 rg\n";
                }

                result += string.Format("{0} TL\n", FontSize);
                result += string.Format("{0} {1} Td\n", curX, PosY);
                result += string.Format("({0}) Tj\n", Text);
                result += "ET";
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
        protected int StringLength(string text, int fontSize, TypeFaces.Barcode font)
        {
            char[] cArray = text.ToUpper().ToCharArray();
            int width = 0;
            foreach (char c in cArray)
            {
                width += font.Metrics[font.CharacterSet.IndexOf(c)];
            }
            return (width * fontSize / 1000);
        }
    }
}
