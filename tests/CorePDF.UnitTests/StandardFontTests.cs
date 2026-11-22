using CorePDF.TypeFaces;
using System;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace CorePDF.UnitTests
{
    [Trait("Category", "Unit Test")]
    public class StandardFontTests
    {
        private static Font Get(string name) => Fonts.Styles().Single(f => f.FontName == name);

        [Theory]
        [InlineData(Fonts.FONTSANSSERIF, "Hello", 722 + 556 + 222 + 222 + 556)]
        [InlineData(Fonts.FONTSANSSERIF, "'`", 191 + 333)] // quotesingle and grave, not the curly quotes
        [InlineData(Fonts.FONTSANSSERIF, "é€©", 556 + 556 + 737)]
        [InlineData(Fonts.FONTSERIF, "Hello", 722 + 444 + 278 + 278 + 500)]
        [InlineData(Fonts.FONTFIXED, "anything", 8 * 600)]
        public void MeasureText_UsesAdobeMetrics(string font, string text, int expected)
        {
            Assert.Equal(expected, Get(font).MeasureText(text));
        }

        [Theory]
        [InlineData("Hello", "(Hello)")]
        [InlineData("Café", "(Caf\\351)")]
        [InlineData("€5 “x”", "(\\2005 \\223x\\224)")]
        [InlineData("a(b)c\\d", "(a\\(b\\)c\\\\d)")]
        [InlineData("soft­hyphen", "(softhyphen)")]
        public void EncodeText_WritesWinAnsiLiteral(string text, string expected)
        {
            Assert.Equal(expected, Get(Fonts.FONTSANSSERIF).EncodeText(text));
        }

        [Fact]
        public void EncodeText_MarksFontAsUsed()
        {
            var font = Get(Fonts.FONTSANSSERIF);
            Assert.False(font.Used);

            font.EncodeText("x");

            Assert.True(font.Used);
        }

        [Theory]
        [InlineData("Ω")]
        [InlineData("中")]
        [InlineData("😀")]
        public void EncodeText_CharacterOutsideWinAnsi_ThrowsHelpfulError(string text)
        {
            var ex = Assert.Throws<ArgumentException>(() => Get(Fonts.FONTSANSSERIF).EncodeText(text));

            Assert.Contains("EmbedFont", ex.Message);
            Assert.Contains("Helvetica", ex.Message);
        }

        [Fact]
        public void SymbolFont_UsesBuiltInEncoding()
        {
            var symbol = Get(Fonts.FONTSYMBOL);
            symbol.Id = "F1";

            using var memStream = new MemoryStream();
            using (var writer = new StreamWriter(memStream))
            {
                symbol.Publish(writer);
            }
            var pdf = Encoding.ASCII.GetString(memStream.ToArray());

            Assert.DoesNotContain("/Encoding", pdf);
            Assert.Equal(250 + 549, symbol.MeasureText(" " + (char)0xB4)); // space and multiply
        }

        [Fact]
        public void TextFonts_CoverEveryWinAnsiCharacter()
        {
            foreach (var font in Fonts.Styles().Where(f => !string.IsNullOrEmpty(f.Encoding)))
            {
                Assert.Equal(256, font.Metrics.Count);
                for (var code = 0x20; code <= 0xFF; code++)
                {
                    // 0x7F, 0x81, 0x8D, 0x8F, 0x90 and 0x9D aren't defined in WinAnsiEncoding
                    if (new[] { 0x7F, 0x81, 0x8D, 0x8F, 0x90, 0x9D }.Contains(code)) continue;
                    Assert.True(font.Metrics[code] > 0, $"{font.FontName} has no width for code {code}");
                }
            }
        }
    }
}
