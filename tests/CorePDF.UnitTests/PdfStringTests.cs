using CorePDF.Conformance;
using CorePDF.Writing;
using System.Text;
using Xunit;

namespace CorePDF.UnitTests
{
    [Trait("Category", "Unit Test")]
    public class PdfStringTests
    {
        [Theory]
        [InlineData("Plain title", "(Plain title)")]
        [InlineData("Has (parens) and \\", "(Has \\(parens\\) and \\\\)")]
        [InlineData("Café", "<FEFF00430061006600E9>")]
        [InlineData("😀", "<FEFFD83DDE00>")]
        public void Text_UsesLiteralForAsciiAndUtf16Otherwise(string value, string expected)
        {
            Assert.Equal(expected, PdfString.Text(value));
        }

        [Theory]
        [InlineData("https://example.com/a?b=c", "(https://example.com/a?b=c)")]
        [InlineData("https://example.com/ü n", "(https://example.com/%C3%BC%20n)")]
        [InlineData("https://example.com/(x)", "(https://example.com/\\(x\\))")]
        public void Uri_IsSevenBitAscii(string value, string expected)
        {
            Assert.Equal(expected, PdfString.Uri(value));
        }

        [Theory]
        [InlineData("ABCDEF+Arial-BoldMT", "ABCDEF+Arial-BoldMT")]
        [InlineData("My Font#1", "My#20Font#231")]
        [InlineData("a/b(c)", "a#2Fb#28c#29")]
        public void Name_EscapesDelimiters(string value, string expected)
        {
            Assert.Equal(expected, PdfString.Name(value));
        }

        [Theory]
        [InlineData('A', 0x41)]
        [InlineData('é', 0xE9)]
        [InlineData('€', 0x80)]
        [InlineData('“', 0x93)]
        [InlineData('Ÿ', 0x9F)]
        public void WinAnsi_MapsUnicodeToCodePage1252(char c, byte expected)
        {
            Assert.True(WinAnsiEncoding.TryEncode(c, out var value));
            Assert.Equal(expected, value);
        }

        [Theory]
        [InlineData('Ω')]
        [InlineData('\u0081')]
        [InlineData('\n')]
        public void WinAnsi_RejectsUnmappableCharacters(char c)
        {
            Assert.False(WinAnsiEncoding.TryEncode(c, out _));
        }

        [Fact]
        public void SrgbProfile_HasValidIccHeader()
        {
            var icc = SrgbIccProfile.Bytes;

            var size = (icc[0] << 24) | (icc[1] << 16) | (icc[2] << 8) | icc[3];
            Assert.Equal(icc.Length, size);
            Assert.Equal(2, icc[8]); // major version
            Assert.Equal("mntrRGB XYZ ", Encoding.ASCII.GetString(icc, 12, 12));
            Assert.Equal("acsp", Encoding.ASCII.GetString(icc, 36, 4));

            var tagCount = (icc[128] << 24) | (icc[129] << 16) | (icc[130] << 8) | icc[131];
            Assert.Equal(9, tagCount);
        }
    }
}
