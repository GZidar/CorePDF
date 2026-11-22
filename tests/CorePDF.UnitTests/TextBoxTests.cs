using CorePDF.Contents;
using CorePDF.Pages;
using CorePDF.TypeFaces;
using System;
using System.IO;
using System.Text;
using Xunit;
using Moq;
using System.Collections.Generic;
using System.Linq;

namespace CorePDF.UnitTests
{
    [Trait("Category", "Unit Test")]
    public class TextBoxTests
    {
        private Mock<PageRoot> _pageRoot = new Mock<PageRoot>();

        private Size _pageSize = new Size
        {
            Width = 500,
            Height = 500,
            Orientation = Orientation.Portrait
        };
        private List<Font> _fonts = Fonts.Styles();

        private TextBox _sut;

        public TextBoxTests()
        {
            var count = 0;
            foreach (var font in _fonts)
            {
                count++;
                font.Id = string.Format("F{0}", count);
            }
            _sut = new TextBox();
        }

        [Theory]
        [InlineData(100, 100, 10, 0, "Helvetica", "#ff0000", "1 0 0 rg")]
        [InlineData(20, 100, 12, 16, "Courier", "#00ff00", "0 1 0 rg")]
        [InlineData(20, 100, 26, 0, "Times-Roman", "#0000ff", "0 0 1 rg")]
        public void CreateTextBox_ExpectSuccess(int posX, int posY, int fontSize, int lineHeight, string fontFace, string color, string colorDef)
        {
            // Arrange
            var result = "";

            _sut.PosX = posX;
            _sut.PosY = posY;
            _sut.FontFace = fontFace;
            _sut.FontSize = fontSize;
            _sut.LineHeight = lineHeight;
            _sut.Color = color;
            _sut.Text = "This is test text";

            _sut.PrepareStream(_pageRoot.Object, _pageSize, _fonts, false);

            var fontDef = string.Format("/{0} {1} Tf", _fonts.Find(f => f.FontName == fontFace).Id, fontSize);
            var lineDef = string.Format("{0} TL", (lineHeight == 0 ? fontSize : lineHeight));
            var startPos = string.Format("{0} {1} Td", posX, posY);
            var textDef = string.Format("({0}) Tj", _sut.Text);

            // Act
            using (var memStream = new MemoryStream())
            {
                using (var writer = new StreamWriter(memStream))
                {
                    _sut.Publish(writer);
                }

                result = Encoding.UTF8.GetString(memStream.ToArray());
            }
            var lines = result.Split('\n');
            int.TryParse(lines.FirstOrDefault(l => l.Contains("/Length"))?.Trim().Substring(7), out int length);
            var instream = false;
            var stream = "";

            foreach (var line in lines)
            {
                if (line.Contains("endstream"))
                {
                    instream = false;
                    break;
                }
                if (line.Contains("stream"))
                {
                    instream = true;
                    continue;
                }
                if (instream)
                {
                    stream += line + "\n";
                }
            }

            // Assert
            Assert.False(string.IsNullOrEmpty(result));
            Assert.Contains(lines, l => l.Contains("/Length"));
            Assert.Equal(length, stream.Length - 1); // use minus one because we get rid of the final newline
            Assert.Contains(lines, l => l.Contains(fontDef));
            Assert.Contains(lines, l => l.Contains(startPos));
            Assert.Contains(lines, l => l.Contains(lineDef));
            Assert.Contains(lines, l => l.Contains(textDef));
            Assert.Contains(lines, l => l.Contains(colorDef));
        }

        [Theory]
        [InlineData(Alignment.Left, 200, 200)]
        [InlineData(Alignment.Right, 134, 200)]
        [InlineData(Alignment.Center, 167, 200)]
        public void CreateTextBox_DifferentAlignment_ExpectSuccess(Alignment aligned, int posX, int posY)
        {
            // Arrange
            var result = "";

            _sut.PosX = 200;
            _sut.PosY = 200;
            _sut.FontFace = "Helvetica";
            _sut.Text = "This is test text";
            _sut.TextAlignment = aligned;

            _sut.PrepareStream(_pageRoot.Object, _pageSize, _fonts, false);

            var textDef = string.Format("({0}) Tj", _sut.Text);
            var startPos = string.Format("{0} {1} Td", posX, posY);

            // Act
            using (var memStream = new MemoryStream())
            {
                using (var writer = new StreamWriter(memStream))
                {
                    _sut.Publish(writer);
                }

                result = Encoding.UTF8.GetString(memStream.ToArray());
            }
            var lines = result.Split('\n');
            int.TryParse(lines.FirstOrDefault(l => l.Contains("/Length"))?.Trim().Substring(7), out int length);
            var instream = false;
            var stream = "";

            foreach (var line in lines)
            {
                if (line.Contains("endstream"))
                {
                    instream = false;
                    break;
                }
                if (line.Contains("stream"))
                {
                    instream = true;
                    continue;
                }
                if (instream)
                {
                    stream += line + "\n";
                }
            }

            // Assert
            Assert.False(string.IsNullOrEmpty(result));
            Assert.Contains(lines, l => l.Contains("/Length"));
            Assert.Equal(length, stream.Length - 1); // use minus one because we get rid of the final newline
            Assert.Contains(lines, l => l.Contains(startPos));
            Assert.Contains(lines, l => l.Contains(textDef));
        }

        private string Prepare(TextBox textBox)
        {
            textBox.PrepareStream(_pageRoot.Object, _pageSize, _fonts, false);
            return textBox.GetEncodedString();
        }

        private string FontId(string name) => _fonts.Find(f => f.FontName == name).Id;

        [Fact]
        public void RightAligned_MixedStyles_MeasuredWithTheStyledFont()
        {
            // "m" is 833 wide in Helvetica but 889 in Helvetica-Bold, so 4 bold m's at 10pt = 35pt
            var stream = Prepare(new TextBox { Text = "<b>mmmm</b>", PosX = 200, PosY = 200, TextAlignment = Alignment.Right });

            Assert.Contains("165 200 Td", stream);
            Assert.Contains(string.Format("/{0} 10 Tf\n(mmmm) Tj", FontId(Fonts.FONTSANSSERIFBOLD)), stream);
        }

        [Fact]
        public void StyleTags_SwitchFontsAndBack()
        {
            var stream = Prepare(new TextBox { Text = "a <B>b</B> <i>c <b>d</b></i> e", PosX = 10, PosY = 200 });

            var expected = string.Join("\n",
                "(a ) Tj",
                $"/{FontId(Fonts.FONTSANSSERIFBOLD)} 10 Tf", "(b) Tj",
                $"/{FontId(Fonts.FONTSANSSERIF)} 10 Tf", "( ) Tj",
                $"/{FontId(Fonts.FONTSANSSERIFITALIC)} 10 Tf", "(c ) Tj",
                $"/{FontId(Fonts.FONTSANSSERIFBOLDITALIC)} 10 Tf", "(d) Tj",
                $"/{FontId(Fonts.FONTSANSSERIF)} 10 Tf", "( e) Tj");
            Assert.Contains(expected, stream);
        }

        [Fact]
        public void Wrapping_AfterAStyleTag_KeepsTheTextIntact()
        {
            var stream = Prepare(new TextBox { Text = "<b>bold</b> words that wrap", PosX = 10, PosY = 200, Width = 40 });

            Assert.DoesNotContain("{", stream);
            Assert.Contains("(bold) Tj", stream);
            Assert.Contains("(words) Tj", stream);
            Assert.Contains("(that) Tj", stream);
            Assert.Contains("(wrap) Tj", stream);
        }

        [Fact]
        public void WindowsLineEndingsAndTabs_AreNormalised()
        {
            var textBox = new TextBox { Text = "one\r\ntwo\tthree", PosX = 10, PosY = 200 };
            var stream = Prepare(textBox);

            Assert.Contains("(one) Tj", stream);
            Assert.Contains("(two three) Tj", stream);
            Assert.DoesNotContain("\r", stream);
            Assert.Equal(20, textBox.Height);
        }

        [Fact]
        public void Height_LimitsTheNumberOfLines()
        {
            var stream = Prepare(new TextBox { Text = "one\ntwo\nthree", PosX = 10, PosY = 200, Height = 15 });

            Assert.Contains("(one) Tj", stream);
            Assert.Contains("(two) Tj", stream);
            Assert.DoesNotContain("three", stream);
        }

        [Fact]
        public void UnknownFont_ThrowsHelpfulError()
        {
            var ex = Assert.Throws<ArgumentException>(() => Prepare(new TextBox { Text = "x", FontFace = "Comic Sans", PosX = 10, PosY = 200 }));

            Assert.Contains("Comic Sans", ex.Message);
        }

        [Fact]
        public void Hyperlink_CoversTheLaidOutText()
        {
            var textBox = new TextBox { Text = "Hello\nWorld!", PosX = 100, PosY = 300, FontSize = 10, Hyperlink = "https://example.com" };
            Prepare(textBox);

            var link = Assert.Single(textBox.GetLinks());
            Assert.Equal("https://example.com", link.Uri);
            Assert.Equal(100, link.Left);
            Assert.Equal(100 + (_fonts[0].MeasureText("World!") * 10 / 1000), link.Right);
            Assert.Equal(310, link.Top); // top line baseline plus the font size
            Assert.Equal(287.5m, link.Bottom); // second line baseline less the descender allowance
        }

        [Fact]
        public void NoHyperlink_HasNoLinks()
        {
            var textBox = new TextBox { Text = "Hello", PosX = 100, PosY = 300 };
            Prepare(textBox);

            Assert.Empty(textBox.GetLinks());
        }
    }
}
