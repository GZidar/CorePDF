using CorePDF.Pages;
using CorePDF.TypeFaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CorePDF.Contents
{
    /// <summary>
    /// Defines any text content to be included in the PDF page
    /// </summary>
    public class TextBox : Content
    {
        /// <summary>
        /// The text to be added to the document. Line breaks start a new line and the
        /// &lt;b&gt; and &lt;i&gt; tags can be used to show text in bold or italics (provided
        /// the bold and italic versions of the font are available).
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

        /// <summary>
        /// A run of text in a single style
        /// </summary>
        private class TextRun
        {
            public string Text;
            public Font Font;
        }

        public override void PrepareStream(PageRoot pageRoot, Size pageSize, List<Font> fonts, bool compress)
        {
            // don't do anything of there is no text
            if (string.IsNullOrEmpty(Text)) return;

            if (PosY < 0 || PosY > pageSize.ContentHeight)
            {
                throw new ArgumentOutOfRangeException("PosY", "TextBox vertical position (PosY) is outside the bounds of the page.");
            }

            var contentFont = fonts.Find(f => f.FontName == FontFace);
            if (contentFont == null)
            {
                throw new ArgumentException($"The font '{FontFace}' is not available. Use one of the standard fonts or embed it using Document.EmbedFont.", nameof(FontFace));
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

            if (textWidth < 0)
            {
                // invalid X coordinates for item
                throw new ArgumentOutOfRangeException("PosX", "TextBox horizontal position (PosX) is outside the bounds of the page.");
            }

            var lines = Layout(contentFont, fonts, textWidth);

            if (Height > 0 && LineHeight > 0)
            {
                // only show the lines that start within the height of the box
                var maxLines = (int)Math.Ceiling(Height / (decimal)LineHeight);
                if (lines.Count > maxLines)
                {
                    lines.RemoveRange(maxLines, lines.Count - maxLines);
                }
            }

            var calcHeight = lines.Count * LineHeight;

            var result = new StringBuilder();
            result.Append("BT\n");
            result.AppendFormat("/{0} {1} Tf\n", contentFont.Id, FontSize);
            result.AppendFormat("{0} TL\n", LineHeight);

            if (!string.IsNullOrEmpty(Color))
            {
                result.AppendFormat("{0} rg\n", ToPDFColor(Color));
            }
            else
            {
                result.Append("0 0 0 rg\n");
            }

            var curY = PosY;
            if (Position == PositionAnchor.Bottom)
            {
                // if the string was split across multiple lines then the Y position needs adjusting
                curY += (calcHeight - LineHeight);
            }
            var topBaseline = curY;

            var count = 0;
            var stringLength = 0;
            var currentFont = contentFont;
            var lineStart = 0;
            var minX = int.MaxValue;
            var maxX = int.MinValue;

            foreach (var line in lines)
            {
                var prevLength = stringLength;
                stringLength = LineLength(line);
                var curX = PosX;

                count++;
                if (count > 1)
                {
                    curY = 0 - LineHeight;
                }

                // text positions (Td) are relative to the start of the previous line
                switch (TextAlignment)
                {
                    case Alignment.Center:
                        curX = count == 1 ? PosX - (stringLength / 2) : (prevLength - stringLength) / 2;
                        break;
                    case Alignment.Right:
                        curX = count == 1 ? PosX - stringLength : prevLength - stringLength;
                        break;
                    case Alignment.Left:
                        if (count > 1)
                        {
                            curX = 0;
                        }
                        break;
                }

                result.AppendFormat("{0} {1} Td\n", curX, curY);

                lineStart = count == 1 ? curX : lineStart + curX;
                minX = Math.Min(minX, lineStart);
                maxX = Math.Max(maxX, lineStart + stringLength);

                foreach (var run in line)
                {
                    if (run.Font != currentFont)
                    {
                        result.AppendFormat("/{0} {1} Tf\n", run.Font.Id, FontSize);
                        currentFont = run.Font;
                    }

                    result.AppendFormat("{0} Tj\n", run.Font.EncodeText(run.Text));
                }
            }

            result.Append("ET");

            if (Height == 0)
            {
                // record the final height of the textbox after it has been rendered
                Height = calcHeight;
            }

            if (lines.Count > 0)
            {
                // the clickable area allows for the ascenders and descenders of the text
                var bottomBaseline = topBaseline - ((lines.Count - 1) * LineHeight);
                SetLinkBounds(minX, bottomBaseline - (FontSize * 0.25m), maxX, topBaseline + FontSize);
            }

            _encodedData = Encoding.UTF8.GetBytes(result.ToString());

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
            return font.MeasureText(text) * fontSize / 1000;
        }

        private int LineLength(List<TextRun> line)
        {
            // the runs share a font size so total their widths before scaling to points
            return line.Sum(r => r.Font.MeasureText(r.Text)) * FontSize / 1000;
        }

        /// <summary>
        /// Splits the text into lines that fit within the width, with each line made up of
        /// runs of text that share the same font.
        /// </summary>
        private List<List<TextRun>> Layout(Font contentFont, List<Font> fonts, int textWidth)
        {
            var result = new List<List<TextRun>>();

            var text = Text.Replace("\r\n", "\n").Replace('\r', '\n').Replace('\t', ' ');
            var bold = false;
            var italic = false;

            Font FontFor(bool b, bool i)
            {
                // a bold or italic font face stays bold or italic regardless of the tags
                b |= contentFont.Bold;
                i |= contentFont.Italic;
                if (b == contentFont.Bold && i == contentFont.Italic) return contentFont;

                return fonts.Find(f => f.BaseFont == contentFont.BaseFont && f.Bold == b && f.Italic == i) ?? contentFont;
            }

            foreach (var paragraph in text.Split('\n'))
            {
                // break the paragraph into words (made up of one or more runs) and the spaces between them
                var words = new List<(List<TextRun> Spaces, List<TextRun> Word)>();
                var spaces = new List<TextRun>();
                var word = new List<TextRun>();
                var buffer = new StringBuilder();

                void Flush()
                {
                    if (buffer.Length > 0)
                    {
                        word.Add(new TextRun { Text = buffer.ToString(), Font = FontFor(bold, italic) });
                        buffer.Clear();
                    }
                }

                void EndWord()
                {
                    Flush();
                    if (word.Count > 0)
                    {
                        words.Add((spaces, word));
                        spaces = new List<TextRun>();
                        word = new List<TextRun>();
                    }
                }

                var pos = 0;
                while (pos < paragraph.Length)
                {
                    var tag = MatchTag(paragraph, pos);
                    if (tag != null)
                    {
                        Flush();
                        if (tag[1] == '/')
                        {
                            if (char.ToLowerInvariant(tag[2]) == 'b') bold = false; else italic = false;
                        }
                        else
                        {
                            if (char.ToLowerInvariant(tag[1]) == 'b') bold = true; else italic = true;
                        }
                        pos += tag.Length;
                        continue;
                    }

                    var c = paragraph[pos];
                    if (c == ' ')
                    {
                        EndWord();
                        spaces.Add(new TextRun { Text = " ", Font = FontFor(bold, italic) });
                    }
                    else
                    {
                        buffer.Append(c);
                    }
                    pos++;
                }
                EndWord();

                // now fill the lines with as many words as will fit
                var line = new List<TextRun>();
                var lineWidth = 0m;
                foreach (var (wordSpaces, wordRuns) in words)
                {
                    var spaceWidth = Measure(wordSpaces);
                    var wordWidth = Measure(wordRuns);

                    if (line.Count > 0 && lineWidth + spaceWidth + wordWidth > textWidth)
                    {
                        // this word would go over the edge so start a new line (the spaces are dropped)
                        result.Add(Merge(line));
                        line = new List<TextRun>();
                        lineWidth = 0;
                    }
                    else
                    {
                        line.AddRange(wordSpaces);
                        lineWidth += spaceWidth;
                    }

                    line.AddRange(wordRuns);
                    lineWidth += wordWidth;
                }

                // keep any trailing spaces at the end of the paragraph
                line.AddRange(spaces);
                result.Add(Merge(line));
            }

            return result;
        }

        private decimal Measure(List<TextRun> runs)
        {
            return runs.Sum(r => r.Font.MeasureText(r.Text)) * FontSize / 1000m;
        }

        /// <summary>
        /// Joins neighbouring runs that use the same font
        /// </summary>
        private static List<TextRun> Merge(List<TextRun> runs)
        {
            var result = new List<TextRun>();
            foreach (var run in runs)
            {
                if (result.Count > 0 && result[result.Count - 1].Font == run.Font)
                {
                    result[result.Count - 1].Text += run.Text;
                }
                else
                {
                    result.Add(new TextRun { Text = run.Text, Font = run.Font });
                }
            }
            return result;
        }

        private static readonly string[] _tags = { "<b>", "</b>", "<i>", "</i>" };

        private static string MatchTag(string text, int pos)
        {
            if (text[pos] != '<') return null;

            foreach (var tag in _tags)
            {
                if (string.Compare(text, pos, tag, 0, tag.Length, StringComparison.OrdinalIgnoreCase) == 0)
                {
                    return text.Substring(pos, tag.Length);
                }
            }
            return null;
        }
    }
}
