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
            Assert.True(lines.Any(l => l.Contains("/Length")));
            Assert.Equal(length, stream.Length - 1); // use minus one because we get rid of the final newline
            Assert.True(lines.Any(l => l.Contains(fontDef)));
            Assert.True(lines.Any(l => l.Contains(startPos)));
            Assert.True(lines.Any(l => l.Contains(lineDef)));
            Assert.True(lines.Any(l => l.Contains(textDef)));
            Assert.True(lines.Any(l => l.Contains(colorDef)));
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
            Assert.True(lines.Any(l => l.Contains("/Length")));
            Assert.Equal(length, stream.Length - 1); // use minus one because we get rid of the final newline
            Assert.True(lines.Any(l => l.Contains(startPos)));
            Assert.True(lines.Any(l => l.Contains(textDef)));
        }


    }
}
