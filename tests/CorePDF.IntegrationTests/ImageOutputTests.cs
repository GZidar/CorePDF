using CorePDF.Contents;
using CorePDF.Embeds;
using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;
using static CorePDF.IntegrationTests.TextOutputTests;

namespace CorePDF.IntegrationTests
{
    /// <summary>
    /// Checks how images are written to the document
    /// </summary>
    [Trait("Category", "Integration")]
    public class ImageOutputTests
    {
        private static PdfInspector PublishImage(ImageFile image, PdfConformance conformance = PdfConformance.None)
        {
            var document = SinglePage(new Image { ImageName = image.Name, PosX = 40, PosY = 400 });
            document.Conformance = conformance;
            document.Images.Add(image);
            return Publish(document);
        }

        [Fact]
        public void Jpeg_IsWrittenUnchanged()
        {
            var file = File.ReadAllBytes("smiley.jpg");

            var pdf = PublishImage(new ImageFile { Name = "smiley", FilePath = "smiley.jpg" });

            var image = Assert.Single(pdf.Objects.Values, o => o.Dictionary.Contains("/Subtype /Image"));
            Assert.Contains("/Filter /DCTDecode", image.Dictionary);
            Assert.Contains("/ColorSpace /DeviceRGB", image.Dictionary);
            Assert.Equal(file, image.Stream);
        }

        [Fact]
        public void Png_WithTransparency_HasCompressedSoftMask()
        {
            var pdf = PublishImage(new ImageFile { Name = "sample", FilePath = "sample.png" });

            var image = Assert.Single(pdf.Objects.Values, o => o.Dictionary.Contains("/SMask"));
            var mask = pdf.Resolve(Regex.Match(image.Dictionary, @"/SMask (\d+ 0 R)").Groups[1].Value);

            Assert.Contains("/Filter /FlateDecode", image.Dictionary);
            Assert.Contains("/Filter /FlateDecode", mask.Dictionary);
            Assert.Contains("/ColorSpace /DeviceGray", mask.Dictionary);
            Assert.Equal(72 * 72 * 3, image.Stream.Length);
            Assert.Equal(72 * 72, mask.Stream.Length);
        }

        [Fact]
        public void Images_AreCompressedEvenWithoutContentCompression()
        {
            var pdf = PublishImage(new ImageFile { Name = "toucan", FilePath = "rpng2-bg16-toucan.png" });

            var image = Assert.Single(pdf.Objects.Values, o => o.Dictionary.Contains("/Subtype /Image"));
            Assert.Contains("/Filter /FlateDecode", image.Dictionary);
            Assert.Equal(170 * 177 * 3, image.Stream.Length);
            Assert.True(pdf.Data.Length < 170 * 177 * 3, "the image data should be compressed");
        }

        [Fact]
        public void Gif_IsDecoded()
        {
            var pdf = PublishImage(new ImageFile { Name = "square", FilePath = "square.gif" });

            var image = pdf.Objects.Values.First(o => o.Dictionary.Contains("/Subtype /Image"));
            Assert.Contains("/Width 3", image.Dictionary);
            Assert.Contains("/Height 3", image.Dictionary);
        }

        [Fact]
        public void UnsupportedImage_ThrowsHelpfulError()
        {
            var image = new ImageFile { Name = "webp", FileData = new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' } };

            var ex = Assert.Throws<NotSupportedException>(() => PublishImage(image));
            Assert.Contains("JPEG, PNG, GIF, BMP and SVG", ex.Message);
        }

        /// <summary>
        /// A JPEG header describing a CMYK image. The image data itself isn't needed, as JPEG
        /// files are passed straight through to the PDF.
        /// </summary>
        private static byte[] CmykJpeg(bool adobe)
        {
            var bytes = new System.Collections.Generic.List<byte> { 0xFF, 0xD8 };
            if (adobe)
            {
                bytes.AddRange(new byte[] { 0xFF, 0xEE, 0, 14, (byte)'A', (byte)'d', (byte)'o', (byte)'b', (byte)'e', 0, 100, 0, 0, 0, 0, 2 });
            }
            bytes.AddRange(new byte[] { 0xFF, 0xC0, 0, 20, 8, 0, 10, 0, 20, 4, 1, 0x11, 0, 2, 0x11, 0, 3, 0x11, 0, 4, 0x11, 0 });
            bytes.AddRange(new byte[] { 0xFF, 0xD9 });
            return bytes.ToArray();
        }

        [Theory]
        [InlineData(false, false)]
        [InlineData(true, true)]
        public void CmykJpeg_IsWrittenAsCmyk(bool adobe, bool inverted)
        {
            var pdf = PublishImage(new ImageFile { Name = "cmyk", FileData = CmykJpeg(adobe) });

            var image = Assert.Single(pdf.Objects.Values, o => o.Dictionary.Contains("/Subtype /Image"));
            Assert.Contains("/ColorSpace /DeviceCMYK", image.Dictionary);
            Assert.Contains("/Width 20", image.Dictionary);
            Assert.Contains("/Height 10", image.Dictionary);
            Assert.Equal(inverted, image.Dictionary.Contains("/Decode [1 0 1 0 1 0 1 0]"));
        }

        [Fact]
        public void CmykJpeg_InPdfA_ThrowsHelpfulError()
        {
            var document = SinglePage(new Image { ImageName = "cmyk", PosX = 40, PosY = 400 });
            document.Conformance = PdfConformance.PdfA2B;
            document.Images.Add(new ImageFile { Name = "cmyk", FileData = CmykJpeg(true) });

            var ex = Assert.Throws<InvalidOperationException>(() => Publish(document));
            Assert.Contains("'cmyk'", ex.Message);
        }

        [Fact]
        public void RawByteData_IsStillSupported()
        {
            // images can be supplied as raw RGB samples instead of a file
            var image = new ImageFile { Name = "raw", ByteData = new byte[] { 255, 0, 0, 0, 255, 0, 0, 0, 255, 255, 255, 255 }, Width = 2, Height = 2 };

            var pdf = PublishImage(image);

            var written = Assert.Single(pdf.Objects.Values, o => o.Dictionary.Contains("/Subtype /Image"));
            Assert.Contains("/ColorSpace /DeviceRGB", written.Dictionary);
            Assert.Equal(image.ByteData, written.Stream);
        }
    }
}
