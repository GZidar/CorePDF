using CorePDF.Embeds;
using Moq;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using Xunit;

namespace CorePDF.UnitTests
{
    [Trait("Category", "Unit Test")]
    public class ConversionTests : IDisposable
    {
        private ImageFile _sut;
        private string _fileName;

        public ConversionTests()
        {
            _sut = new ImageFile();
            _fileName = DateTime.Now.Ticks.ToString() + ".svg";
            _sut.FilePath = _fileName;
        }

        // create the file used in the tests
        private void createFile(string contents)
        {
            using (var filestream = new FileStream(_fileName, FileMode.Create, FileAccess.Write))
            {
                var fileData = @"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 1000 1000"">" + contents + "</svg>";
                filestream.Write(Encoding.UTF8.GetBytes(fileData), 0, fileData.Length);
                filestream.Flush();
            }
        }

        public void Dispose()
        {
            // clean up any files that might have been created by the tests
            if (File.Exists(_fileName))
            {
                File.Delete(_fileName);
            }
        }

        [Fact]
        public void BasicSVGFile_ExpectSuccess()
        {
            // Arrange
            var content = @"<path d=""M50,50l50,50h50z""/>";
            createFile(content);
            Assert.True(File.Exists(_fileName));

            // Act
            _sut.EmbedFile();
            var result = JsonConvert.DeserializeObject<TokenisedSVG>(Encoding.UTF8.GetString(_sut.ByteData));

            // Assert
            Assert.Equal(ImageFile.PATHDATA, _sut.Type);
            Assert.Equal(1000, _sut.Height);
            Assert.Equal(1000, _sut.Width);
            Assert.True(result.Paths.Any(p => p.ToString().Contains("50 950 m")));
            Assert.True(result.Paths.Any(p => p.ToString().Contains("100 900 l")));
            Assert.True(result.Paths.Any(p => p.ToString().Contains("150 900 l")));
            Assert.True(result.Paths.Any(p => p.ToString().Contains("50 950 l")));
        }

        [Theory]
        [InlineData("M100,200 C100,100 400,100 400,200", "100 800 m", "100 900 400 900 400 800 c")]
        [InlineData("M100,500 C25,400 475,400 400,500", "100 500 m", "25 600 475 600 400 500 c")]
        public void ConvertPath_CubicBezier_ExpectSuccess(string path, string position, string curve)
        {
            // Arrange
            var content = string.Format(@"<path d=""{0}""/>", path);
            createFile(content);
            Assert.True(File.Exists(_fileName));

            // Act
            _sut.EmbedFile();
            var result = JsonConvert.DeserializeObject<TokenisedSVG>(Encoding.UTF8.GetString(_sut.ByteData));

            // Assert
            Assert.True(result.Paths.Any(p => p.ToString().Contains(position)));
            Assert.True(result.Paths.Any(p => p.ToString().Contains(curve)));
        }
    }
}
