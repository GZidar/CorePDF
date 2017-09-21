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
    public class PolygonTests
    {
        private Mock<Size> _pageSize = new Mock<Size>();
        private Mock<PageRoot> _pageRoot = new Mock<PageRoot>();
        private Mock<List<Font>> _fonts = new Mock<List<Font>>();

        private Shape _sut;

        public PolygonTests()
        {
            _sut = new Shape();
        }

        [Theory]
        [InlineData(100, 100, 100, 200)]
        [InlineData(0, 100, 100, 20)]
        [InlineData(200, 300, 300, 200)]
        [InlineData(0, 0, 200, 100)]
        public void CreateRectangle_WithSquareCorners_ExpectSuccess(int posX, int posY, int height, int width)
        {
            // Arrange
            var result = "";

            _sut.Type = Polygon.Rectangle;
            _sut.BorderRadius = 0; // this means square corners
            _sut.PosX = posX;
            _sut.PosY = posY;
            _sut.Width = width;
            _sut.Height = height;
            _sut.PrepareStream(_pageRoot.Object, _pageSize.Object, _fonts.Object, false);

            var polygon = string.Format("{0} {1} {2} {3} re", posX, posY, width, height);

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
            Assert.True(lines.Any(l => l.Contains(polygon)));
        }

        [Theory]
        [InlineData("#ff0000", "1 0 0 rg")]
        [InlineData("#00ff00", "0 1 0 rg")]
        [InlineData("#0000ff", "0 0 1 rg")]
        [InlineData("#282828", "0.16 0.16 0.16 rg")]
        [InlineData("#000000", "0 0 0 rg")]
        public void CreateLine_WithFillColor_ExpectSuccess(string color, string colorDef)
        {
            // Arrange
            var result = "";

            _sut.Type = Polygon.Line;
            _sut.BorderRadius = 0; // this means square corners
            _sut.PosX = 100;
            _sut.PosY = 100;
            _sut.Width = 200;
            _sut.Height = 300;
            _sut.FillColor = color;
            _sut.PrepareStream(_pageRoot.Object, _pageSize.Object, _fonts.Object, false);

            var strokeDef = string.Format("{0} w", 1);

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
            Assert.True(lines.Any(l => l.Contains(strokeDef)));
            Assert.True(lines.Any(l => l.Contains(colorDef)));
        }


        [Theory]
        [InlineData(100, 100, 100, 200, 5)]
        [InlineData(0, 100, 100, 20, 2)]
        [InlineData(200, 300, 300, 200, 15)]
        [InlineData(0, 0, 200, 100, 8)]
        public void CreateRectangle_WithRoundedCorners_ExpectSuccess(int posX, int posY, int height, int width, int radius)
        {
            // Arrange
            var result = "";

            _sut.Type = Polygon.Rectangle;
            _sut.PosX = posX;
            _sut.PosY = posY;
            _sut.Width = width;
            _sut.Height = height;
            _sut.BorderRadius = radius;
            _sut.PrepareStream(_pageRoot.Object, _pageSize.Object, _fonts.Object, false);

            var startPos = string.Format("{0} {1} m", posX, posY + radius);
            var path1 = string.Format("{0} {1} l", posX, posY + height - radius);
            var path2 = string.Format("{0} {1} l", posX + width - radius, posY + height);
            var path3 = string.Format("{0} {1} l", posX + width, posY + radius);
            var path4 = string.Format("{0} {1} l", posX + radius, posY);

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
            Assert.True(lines.Any(l => l.Contains(path1)));
            Assert.True(lines.Any(l => l.Contains(path2)));
            Assert.True(lines.Any(l => l.Contains(path3)));
            Assert.True(lines.Any(l => l.Contains(path4)));
        }

        [Theory]
        [InlineData(100, 100, 0, 200)]
        [InlineData(0, 100, 100, 0)]
        [InlineData(200, 300, 300, 200)]
        [InlineData(0, 0, 200, 100)]
        public void CreateLine_ExpectSuccess(int posX, int posY, int height, int width)
        {
            // Arrange
            var result = "";

            _sut.Type = Polygon.Line;
            _sut.BorderRadius = 0; // this means square corners
            _sut.PosX = posX;
            _sut.PosY = posY;
            _sut.Width = width;
            _sut.Height = height;
            _sut.PrepareStream(_pageRoot.Object, _pageSize.Object, _fonts.Object, false);

            var startPos = string.Format("{0} {1} m", posX, posY);
            var path = string.Format("{0} {1} l", posX + width, posY + height);

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
            Assert.True(lines.Any(l => l.Contains(path)));
        }

        [Theory]
        [InlineData(1, "#ff0000", "1 0 0 RG")]
        [InlineData(2, "#00ff00", "0 1 0 RG")]
        [InlineData(4, "#0000ff", "0 0 1 RG")]
        [InlineData(6, "#282828", "0.16 0.16 0.16 RG")]
        [InlineData(8, "#000000", "0 0 0 RG")]
        public void CreateLine_WithStrokeAndColor_ExpectSuccess(int stroke, string color, string colorDef)
        {
            // Arrange
            var result = "";

            _sut.Type = Polygon.Line;
            _sut.BorderRadius = 0; // this means square corners
            _sut.PosX = 100;
            _sut.PosY = 100;
            _sut.Width = 200;
            _sut.Height = 0;
            _sut.Stroke = new Stroke
            {
                Width = stroke,
                Color = color
            };
            _sut.PrepareStream(_pageRoot.Object, _pageSize.Object, _fonts.Object, false);

            var strokeDef = string.Format("{0} w", stroke);

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
            Assert.True(lines.Any(l => l.Contains(strokeDef)));
            Assert.True(lines.Any(l => l.Contains(colorDef)));
        }


        [Theory]
        [InlineData(200, 300, 20, 30)]
        [InlineData(300, 200, 30, 20)]
        [InlineData(100, 150, 40, 50)]
        [InlineData(250, 500, 40, 40)]
        public void CreateEllipse_ExpectSuccess(int posX, int posY, int width, int height)
        {
            // Arrange
            var result = "";

            _sut.Type = Polygon.Ellipses;
            _sut.PosX = posX;
            _sut.PosY = posY;
            _sut.Width = width;
            _sut.Height = height;
            _sut.PrepareStream(_pageRoot.Object, _pageSize.Object, _fonts.Object, false);

            var startPos = string.Format("{0} {1} m", posX - (width / 2), posY);
            var path1 = string.Format("{0} {1} c", posX, posY + (height / 2));
            var path2 = string.Format("{0} {1} c", posX + (width / 2), posY);
            var path3 = string.Format("{0} {1} c", posX, posY - (height / 2));
            var path4 = string.Format("{0} {1} c", posX - (width / 2), posY);

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

            Assert.False(string.IsNullOrEmpty(result));
            Assert.True(lines.Any(l => l.Contains("/Length")));
            Assert.Equal(length, stream.Length - 1); // use minus one because we get rid of the final newline
            Assert.True(lines.Any(l => l.Contains(startPos)));
            Assert.True(lines.Any(l => l.Contains(path1)));
            Assert.True(lines.Any(l => l.Contains(path2)));
            Assert.True(lines.Any(l => l.Contains(path3)));
            Assert.True(lines.Any(l => l.Contains(path4)));
        }
    }
}
