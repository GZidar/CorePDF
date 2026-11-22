using CorePDF.Embeds;
using CorePDF.Svg;
using System;
using System.IO;
using System.Text;
using Xunit;

namespace CorePDF.UnitTests
{
    /// <summary>
    /// Checks the exact drawing operations SVG paths are converted to. The drawing is written in
    /// SVG co-ordinates (y down): the flip to PDF's co-ordinates is a single transform at the start.
    /// </summary>
    [Trait("Category", "Unit Test")]
    public class ConversionTests
    {
        private static SvgDrawing Convert(string contents, string attributes = @"viewBox=""0 0 1000 1000""")
        {
            var svg = $@"<svg xmlns=""http://www.w3.org/2000/svg"" {attributes}>{contents}</svg>";
            return SvgConverter.Convert(Encoding.UTF8.GetBytes(svg));
        }

        [Fact]
        public void BasicSVGFile_ExpectSuccess()
        {
            var image = new ImageFile { FileData = Encoding.UTF8.GetBytes(@"<svg xmlns=""http://www.w3.org/2000/svg"" viewBox=""0 0 1000 1000""><path d=""M50,50l50,50h50z""/></svg>") };

            image.EmbedFile();

            Assert.Equal(ImageFile.PATHDATA, image.Type);
            Assert.Equal(1000, image.Height);
            Assert.Equal(1000, image.Width);

            var content = image.Drawing.Content;
            Assert.StartsWith("1 0 0 -1 0 1000 cm\n", content);
            Assert.Contains("50 50 m\n100 100 l\n150 100 l\nh\n", content);
            Assert.Contains("0 0 0 rg\n", content); // the default fill is black
            Assert.EndsWith("f\nQ\n", content);
        }

        [Theory]
        [InlineData("M100,200 C100,100 400,100 400,200", "100 200 m", "100 100 400 100 400 200 c")]
        [InlineData("M100,500 C25,400 475,400 400,500", "100 500 m", "25 400 475 400 400 500 c")]
        [InlineData("M600,800 C625,700 725,700 750,800S875, 900 900, 800", "600 800 m", "625 700 725 700 750 800 c", "775 900 875 900 900 800 c")]
        public void ConvertPath_CubicBezier_ExpectSuccess(string path, string position, params string[] curves)
        {
            var content = Convert($@"<path d=""{path}""/>").Content;

            Assert.Contains(position, content);
            foreach (var curve in curves)
            {
                Assert.Contains(curve, content);
            }
        }

        [Theory]
        [InlineData("M200, 300 Q400, 50 600, 300 T1000, 300", "200 300 m", "333.3333 133.3333 466.6667 133.3333 600 300 c", "733.3333 466.6667 866.6667 466.6667 1000 300 c")]
        public void ConvertPath_QuadraticBezier_ExpectSuccess(string path, string position, params string[] curves)
        {
            var content = Convert($@"<path d=""{path}""/>").Content;

            Assert.Contains(position, content);
            foreach (var curve in curves)
            {
                Assert.Contains(curve, content);
            }
        }

        [Theory]
        [InlineData("M300,200 h-150 a150,150 0 1,0 150,-150 z", "300 200 m", "150 282.8427 217.1573 350 300 350 c", "382.8427 350 450 282.8427 450 200 c", "450 117.1573 382.8427 50 300 50 c")]
        [InlineData("M275,175 v-150 a150,150 0 0,0 -150,150 z", "275 175 m", "192.1573 25 125 92.1573 125 175 c")]
        public void ConvertPath_Arc_ExpectSuccess(string path, string position, params string[] curves)
        {
            var content = Convert($@"<path d=""{path}""/>").Content;

            Assert.Contains(position, content);
            foreach (var curve in curves)
            {
                Assert.Contains(curve, content);
            }
        }

        [Theory]
        [InlineData("M10 10 20 20 30 10", "10 10 m\n20 20 l\n30 10 l\n")] // pairs after a move are lines
        [InlineData("m10 10 10 10 10 -10", "10 10 m\n20 20 l\n30 10 l\n")]
        [InlineData("M10-5L20.5.5", "10 -5 m\n20.5 0.5 l\n")] // compact numbers
        [InlineData("M1e1 2E1l1e-1 0", "10 20 m\n10.1 20 l\n")] // exponents
        [InlineData("M10 10 h5 5 v5 5", "15 10 l\n20 10 l\n20 15 l\n20 20 l\n")]
        [InlineData("M0 0 a0 5 0 0 1 10 0", "0 0 m\n10 0 l\n")] // an arc with no radius is a line
        [InlineData("M0 0 L10 0 Z L5 5", "h\n0 0 m\n5 5 l\n")] // drawing after a close starts a new subpath
        public void PathGrammar_ExpectSuccess(string path, string expected)
        {
            var content = Convert($@"<path d=""{path}"" stroke=""black"" fill=""none""/>").Content;

            Assert.Contains(expected, content);
        }

        [Fact]
        public void PathGrammar_CompactArcFlags_ExpectSuccess()
        {
            // flags don't need separating: "1010 0" is large-arc 1, sweep 0, then x = 10, y = 0
            var compact = Convert(@"<path d=""M0 0a5 5 0 1010 0"" stroke=""black""/>").Content;
            var spaced = Convert(@"<path d=""M0 0a5 5 0 1 0 10 0"" stroke=""black""/>").Content;

            Assert.Equal(spaced, compact);
        }

        [Fact]
        public void PathGrammar_StopsAtAnError()
        {
            // the specification says to draw the path up to the error
            var content = Convert(@"<path d=""M0 0 L10 10 L20 x L30 30"" stroke=""black""/>").Content;

            Assert.Contains("10 10 l\n", content);
            Assert.DoesNotContain("30 30", content);
        }

        [Theory]
        [InlineData(@"width=""100"" height=""50""", 100, 50)]
        [InlineData(@"width=""1in"" height=""72pt""", 96, 96)]
        [InlineData(@"width=""25.4mm"" height=""2.54cm""", 96, 96)]
        [InlineData(@"viewBox=""0 0 24 12""", 24, 12)]
        [InlineData(@"width=""48"" viewBox=""0 0 24 12""", 48, 24)]
        [InlineData(@"width=""100%"" height=""100%"" viewBox=""0 0 30 20""", 30, 20)]
        [InlineData("", 300, 150)]
        public void Size_ExpectSuccess(string attributes, double width, double height)
        {
            var drawing = Convert("", attributes);

            Assert.Equal(width, drawing.Width, 3);
            Assert.Equal(height, drawing.Height, 3);
        }

        [Theory]
        [InlineData("<text x='0' y='10'>Hello</text>", "text")]
        [InlineData("<image href='a.png' width='10' height='10'/>", "image")]
        [InlineData("<pattern id='p'/><rect width='10' height='10' fill='url(#p)'/>", "patterns")]
        [InlineData("<mask id='m'/><rect width='10' height='10' mask='url(#m)'/>", "masks")]
        [InlineData("<filter id='f'/><rect width='10' height='10' filter='url(#f)'/>", "filters")]
        [InlineData("<marker id='m'/><path d='M0 0 L10 10' stroke='black' marker-end='url(#m)'/>", "markers")]
        public void UnsupportedFeatures_ThrowHelpfulErrors(string contents, string feature)
        {
            var ex = Assert.Throws<NotSupportedException>(() => Convert(contents));

            Assert.Contains(feature, ex.Message);
        }

        [Fact]
        public void LinearGradient_IsAnAxialShadingPattern()
        {
            var drawing = Convert(@"<linearGradient id='g'><stop offset='0' stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient>
                <rect x='10' y='20' width='100' height='50' fill='url(#g)'/>");

            Assert.Contains("/Pattern cs /P1 scn\n", drawing.Content);
            var pattern = Assert.Single(drawing.Patterns);
            Assert.Contains("/ShadingType 2", pattern);
            Assert.Contains("/Coords [0 0 1 0]", pattern);
            Assert.Contains("/C0 [1 0 0] /C1 [0 0 1]", pattern);
            // bounding box units map the gradient onto the rectangle (in a y-flipped 1000 high viewBox)
            Assert.Contains("/Matrix [100 0 0 -50 10 980]", pattern);
            Assert.Empty(drawing.Forms);
        }

        [Fact]
        public void RadialGradient_IsARadialShadingPattern()
        {
            var drawing = Convert(@"<radialGradient id='g' fx='0.25'><stop offset='0' stop-color='white'/><stop offset='1' stop-color='black'/></radialGradient>
                <circle cx='50' cy='50' r='50' fill='url(#g)'/>");

            var pattern = Assert.Single(drawing.Patterns);
            Assert.Contains("/ShadingType 3", pattern);
            Assert.Contains("/Coords [0.25 0.5 0 0.5 0.5 0.5]", pattern);
        }

        [Fact]
        public void GradientStops_OutsideZeroToOne_KeepTheirEndColours()
        {
            var drawing = Convert(@"<linearGradient id='g'><stop offset='0.25' stop-color='red'/><stop offset='0.75' stop-color='blue'/></linearGradient>
                <rect width='10' height='10' fill='url(#g)'/>");

            // red until the first stop, then red to blue, then blue after the last stop
            var pattern = Assert.Single(drawing.Patterns);
            Assert.Contains("/C0 [1 0 0] /C1 [1 0 0]", pattern);
            Assert.Contains("/C0 [1 0 0] /C1 [0 0 1]", pattern);
            Assert.Contains("/C0 [0 0 1] /C1 [0 0 1]", pattern);
            Assert.Contains("/Bounds [0.25 0.75]", pattern);
        }

        [Fact]
        public void GradientWithVaryingOpacity_UsesASoftMask()
        {
            var drawing = Convert(@"<linearGradient id='g'><stop offset='0' stop-color='red'/><stop offset='1' stop-color='red' stop-opacity='0'/></linearGradient>
                <rect width='10' height='10' fill='url(#g)'/>");

            var mask = Assert.Single(drawing.Forms);
            Assert.Contains("/CS /DeviceGray", mask.Group);
            Assert.Contains("/Sh1 sh", mask.Content);
            Assert.Contains("/ColorSpace /DeviceGray", Assert.Single(drawing.Shadings));
            Assert.Contains(drawing.GraphicsStates, g => g.Contains("/SMask << /Type /Mask /S /Luminosity /G {Fm1} >>"));
        }

        [Fact]
        public void GradientWithUniformOpacity_UsesConstantOpacity()
        {
            var drawing = Convert(@"<linearGradient id='g'><stop offset='0' stop-color='red' stop-opacity='0.5'/><stop offset='1' stop-color='blue' stop-opacity='0.5'/></linearGradient>
                <rect width='10' height='10' fill='url(#g)'/>");

            Assert.Empty(drawing.Forms);
            Assert.Contains(drawing.GraphicsStates, g => g.Contains("/ca 0.5"));
        }

        [Theory]
        [InlineData("<linearGradient id='g'><stop offset='0' stop-color='red'/></linearGradient>", "1 0 0 rg")] // a single stop
        [InlineData("<linearGradient id='g' x2='0'><stop offset='0' stop-color='red'/><stop offset='1' stop-color='blue'/></linearGradient>", "0 0 1 rg")] // no length: the last stop
        [InlineData("<radialGradient id='g' r='0'><stop offset='0' stop-color='red'/><stop offset='1' stop-color='lime'/></radialGradient>", "0 1 0 rg")] // no radius: the last stop
        public void GradientsThatCantVary_AreSolid(string gradient, string expected)
        {
            var drawing = Convert(gradient + "<rect width='10' height='10' fill='url(#g)'/>");

            Assert.Contains(expected, drawing.Content);
            Assert.Empty(drawing.Patterns);
        }

        [Fact]
        public void GradientWithNoStops_PaintsNothing()
        {
            var drawing = Convert("<linearGradient id='g'/><rect width='10' height='10' fill='url(#g)'/>");

            Assert.DoesNotContain(" re", drawing.Content);
            Assert.DoesNotContain(" f\n", drawing.Content);
        }

        [Fact]
        public void MissingPaintReference_UsesTheFallbackColour()
        {
            Assert.Contains("0 0 1 rg", Convert("<rect width='10' height='10' fill='url(#missing) blue'/>").Content);
            Assert.DoesNotContain(" rg", Convert("<rect width='10' height='10' fill='url(#missing)'/>").Content);
        }

        [Fact]
        public void NotSvg_ThrowsInvalidData()
        {
            Assert.Throws<InvalidDataException>(() => SvgConverter.Convert(Encoding.UTF8.GetBytes("<html><svg></svg></html>")));
            Assert.Throws<InvalidDataException>(() => SvgConverter.Convert(Encoding.UTF8.GetBytes("<svg><unclosed></svg>")));
        }

        [Fact]
        public void Doctype_IsIgnored()
        {
            var svg = @"<?xml version=""1.0""?>
<!DOCTYPE svg PUBLIC ""-//W3C//DTD SVG 1.1//EN"" ""http://www.w3.org/Graphics/SVG/1.1/DTD/svg11.dtd"">
<svg xmlns=""http://www.w3.org/2000/svg"" width=""10"" height=""10""><rect width=""10"" height=""10""/></svg>";

            var drawing = SvgConverter.Convert(Encoding.UTF8.GetBytes(svg));

            Assert.Equal(10, drawing.Width);
        }
    }
}
