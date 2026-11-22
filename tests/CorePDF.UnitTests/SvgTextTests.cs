using CorePDF.Embeds;
using CorePDF.Svg;
using CorePDF.TypeFaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace CorePDF.UnitTests
{
    [Trait("Category", "Unit Test")]
    public class SvgTextTests
    {
        private readonly List<Font> _fonts;

        public SvgTextTests()
        {
            // the standard fonts plus an embedded family with a (pretend) bold variant
            var ttf = new TrueTypeFont(File.ReadAllBytes("GreatVibes-Regular.ttf"));
            _fonts = Fonts.Styles();
            _fonts.Add(new EmbeddedFont(new FontFile { Name = "Vibes", BaseFont = "Vibes Family" }, ttf));
            _fonts.Add(new EmbeddedFont(new FontFile { Name = "Vibes-Bold", BaseFont = "Vibes Family", Bold = true }, ttf));
            for (var i = 0; i < _fonts.Count; i++) _fonts[i].Id = "F" + (i + 1);
        }

        private string Id(string fontName) => _fonts.Single(f => f.FontName == fontName).Id;

        private SvgDrawing Convert(string contents)
        {
            var svg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" width=""200"" height=""100"">{contents}</svg>";
            return SvgConverter.Convert(Encoding.UTF8.GetBytes(svg), _fonts);
        }

        [Theory]
        [InlineData("Vibes", "Vibes")] // an embedded font by name
        [InlineData("Vibes Family", "Vibes")] // or by its family (BaseFont)
        [InlineData("'Missing Font', Vibes", "Vibes")] // the first family that is available
        [InlineData("Arial", Fonts.FONTSANSSERIF)]
        [InlineData("Helvetica, sans-serif", Fonts.FONTSANSSERIF)]
        [InlineData("'Times New Roman'", Fonts.FONTSERIF)]
        [InlineData("monospace", Fonts.FONTFIXED)]
        [InlineData("Some Unknown Font", Fonts.FONTSERIF)] // the browser default
        public void FontFamily_IsResolved(string family, string expected)
        {
            var drawing = Convert($@"<text x=""10"" y=""50"" font-family=""{family}"">Hello</text>");

            Assert.Contains($"/{Id(expected)} 16 Tf", drawing.Content);
            Assert.Contains(drawing.Fonts, f => f.FontName == expected);
        }

        [Theory]
        [InlineData("Arial", @"font-weight=""bold""", Fonts.FONTSANSSERIFBOLD)]
        [InlineData("Arial", @"font-weight=""700"" font-style=""italic""", Fonts.FONTSANSSERIFBOLDITALIC)]
        [InlineData("serif", @"font-style=""oblique""", Fonts.FONTSERIFITALIC)]
        [InlineData("Vibes", @"font-weight=""bold""", "Vibes-Bold")]
        [InlineData("Vibes", @"font-style=""italic""", "Vibes")] // no italic variant: the regular one
        public void FontWeightAndStyle_SelectTheVariant(string family, string attributes, string expected)
        {
            var drawing = Convert($@"<text x=""10"" y=""50"" font-family=""{family}"" {attributes}>Hello</text>");

            Assert.Contains($"/{Id(expected)} 16 Tf", drawing.Content);
        }

        [Fact]
        public void Text_IsFlippedUprightAtItsPosition()
        {
            var drawing = Convert(@"<text x=""10"" y=""50"" font-family=""Arial"" font-size=""20"">Hi</text>");

            // the drawing is in SVG co-ordinates (y down), so the text matrix flips the glyphs upright
            Assert.Contains("1 0 0 -1 10 50 Tm\n[(Hi)] TJ\n", drawing.Content);
        }

        [Fact]
        public void TextAnchor_AlignsUsingTheFontMetrics()
        {
            // "Hi" in Helvetica is 722 + 222 thousandths of the font size
            var middle = Convert(@"<text x=""100"" y=""50"" font-family=""Arial"" font-size=""10"" text-anchor=""middle"">Hi</text>");
            var end = Convert(@"<text x=""100"" y=""50"" font-family=""Arial"" font-size=""10"" text-anchor=""end"">Hi</text>");

            Assert.Contains("1 0 0 -1 95.28 50 Tm", middle.Content);
            Assert.Contains("1 0 0 -1 90.56 50 Tm", end.Content);
        }

        [Fact]
        public void LetterSpacing_IsWrittenAsAdjustments()
        {
            var drawing = Convert(@"<text x=""10"" y=""50"" font-family=""Arial"" font-size=""10"" letter-spacing=""2"">abc</text>");

            // 2 units at a font size of 10 is 200 thousandths, moving right (so negative)
            Assert.Contains("[(a) -200 (b) -200 (c)] TJ", drawing.Content);
        }

        [Theory]
        [InlineData("auto", 50)]
        [InlineData("middle", 52.615)] // half the x-height (523) below
        [InlineData("central", 52.555)] // half way between the ascent (718) and descent (-207)
        [InlineData("text-before-edge", 57.18)] // the ascent below
        [InlineData("text-after-edge", 47.93)] // the descent above
        public void DominantBaseline_ShiftsTheBaseline(string baseline, double expectedY)
        {
            var drawing = Convert($@"<text x=""10"" y=""50"" font-family=""Arial"" font-size=""10"" dominant-baseline=""{baseline}"">x</text>");

            Assert.Contains($"1 0 0 -1 10 {SvgNumber.Format(expectedY)} Tm", drawing.Content);
        }

        [Fact]
        public void TextFillAndStroke_UseTheTextRenderingMode()
        {
            Assert.DoesNotContain(" Tr", Convert(@"<text x=""0"" y=""20"" font-family=""Arial"">a</text>").Content);
            Assert.Contains("1 Tr", Convert(@"<text x=""0"" y=""20"" font-family=""Arial"" fill=""none"" stroke=""red"">a</text>").Content);
            Assert.Contains("2 Tr", Convert(@"<text x=""0"" y=""20"" font-family=""Arial"" stroke=""red"">a</text>").Content);
        }

        [Fact]
        public void EmbeddedFontText_IsWrittenAsGlyphIds()
        {
            var drawing = Convert(@"<text x=""0"" y=""20"" font-family=""Vibes"">Zażółć</text>");

            Assert.Matches(@"\[<[0-9A-F]{24}>\] TJ", drawing.Content);
            Assert.True(_fonts.Single(f => f.FontName == "Vibes").Used);
        }

        [Fact]
        public void CharacterNotInTheFont_ThrowsHelpfulError()
        {
            var ex = Assert.Throws<ArgumentException>(() => Convert(@"<text x=""0"" y=""20"" font-family=""Arial"">Ωmega</text>"));

            Assert.Contains("EmbedFont", ex.Message);
        }

        [Theory]
        [InlineData(@"<path id=""p"" d=""M0 0 L100 0""/><text><textPath href=""#p"">On a path</textPath></text>", "textPath")]
        [InlineData(@"<text x=""0"" y=""20"" rotate=""10"">Rotated</text>", "rotate")]
        public void UnsupportedTextFeatures_ThrowHelpfulErrors(string contents, string feature)
        {
            var ex = Assert.Throws<NotSupportedException>(() => Convert(contents));

            Assert.Contains(feature, ex.Message);
        }

        [Fact]
        public void TextWithoutTheDocumentsFonts_ThrowsHelpfulError()
        {
            var svg = @"<svg xmlns=""http://www.w3.org/2000/svg"" width=""10"" height=""10""><text>Hi</text></svg>";

            var ex = Assert.Throws<NotSupportedException>(() => SvgConverter.Convert(Encoding.UTF8.GetBytes(svg)));
            Assert.Contains("document", ex.Message);
        }
    }
}
