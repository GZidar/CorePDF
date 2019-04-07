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
    public class ImageTests
    {
        private Mock<Size> _pageSize = new Mock<Size>();
        private Mock<PageRoot> _pageRoot = new Mock<PageRoot>();
        private Mock<List<Font>> _fonts = new Mock<List<Font>>();
        private Mock<Document> _document = new Mock<Document>();

        private Image _sut;

        public ImageTests()
        {
            var byteData = new byte[27];

            byteData[0] = 255;
            byteData[1] = 0;
            byteData[2] = 0;
            byteData[3] = 0;
            byteData[4] = 255;
            byteData[5] = 0;
            byteData[6] = 0;
            byteData[7] = 0;
            byteData[8] = 255;
            byteData[9] = 0;
            byteData[10] = 0;
            byteData[11] = 0;
            byteData[12] = 255;
            byteData[13] = 255;
            byteData[14] = 255;
            byteData[15] = 128;
            byteData[16] = 128;
            byteData[17] = 128;
            byteData[18] = 255;
            byteData[19] = 0;
            byteData[20] = 0;
            byteData[21] = 255;
            byteData[22] = 0;
            byteData[23] = 0;
            byteData[24] = 255;
            byteData[25] = 0;
            byteData[26] = 0;

            _pageRoot.SetupGet(pr => pr.Document).Returns(_document.Object);
            _document.Setup(d => d.GetImage(It.IsAny<string>())).Returns(new Embeds.ImageFile
            {
                Id = "I1",
                ByteData = byteData,
                Width = 3,
                Height = 3,
                Type = Embeds.ImageFile.IMAGEDATA
            });

            _sut = new Image();
        }

        [Theory]
        [InlineData(100, 200, 1)]
        [InlineData(100, 200, 10)]
        public void CreateImage_ExpectSuccess(int posX, int posY, decimal scale)
        {
            // Arrange
            var result = "";

            _sut.PosX = posX;
            _sut.PosY = posY;
            _sut.ScaleFactor = scale;

            _sut.PrepareStream(_pageRoot.Object, _pageSize.Object, _fonts.Object, false);

            var imageDef = "/I1 Do";
            var placement = string.Format("{0} 0 0 {0} {1} {2} cm", scale * 3, posX, posY); // the 3 comes from the image definition in the constructor

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
            Assert.Contains(lines, l => l.Contains(imageDef));
            Assert.Contains(lines, l => l.Contains(placement));
        }

        [Fact]
        public void ProcessSVG_ExpectSuccess()
        {

        }
    }
}
