using CorePDF.Contents;
using CorePDF.Embeds;
using SkiaSharp;
using Svg.Skia;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static CorePDF.IntegrationTests.TextOutputTests;

namespace CorePDF.IntegrationTests.Svg
{
    /// <summary>
    /// Measures how accurately SVG images are converted into PDF drawing operations, by
    /// comparing our output with a reference rendering of the same SVG (Svg.Skia, used only
    /// in tests). Both are rasterised with SkiaSharp, so differences come from the conversion
    /// rather than from two different renderers. Renderings of any file that doesn't match
    /// are saved in the svg-diffs folder of the test output.
    ///
    /// Note: the reference rounds an image's size to whole pixels (eg: 40mm, which is 151.18px,
    /// becomes 151px) and fits the viewBox to that, whereas browsers and CorePDF use the exact
    /// size. To keep that from showing up as differences, corpus images have sizes that are a
    /// whole number of pixels; exact unit conversions are covered by the unit tests.
    /// </summary>
    [Trait("Category", "Integration")]
    public class SvgAccuracyTests
    {
        private const string CORPUS = "Svg/Corpus";

        /// <summary>
        /// The largest side of the comparison images, in pixels
        /// </summary>
        private const int RENDERSIZE = 400;

        /// <summary>
        /// A pixel counts as different if any channel differs by more than this (out of 255),
        /// which ignores small anti-aliasing differences
        /// </summary>
        private const int PIXELTOLERANCE = 64;

        /// <summary>
        /// The share of pixels allowed to differ
        /// </summary>
        private const double MAXDIFFERENT = 0.005;

        public static IEnumerable<object[]> CorpusFiles()
        {
            return Directory.GetFiles(CORPUS, "*.svg").OrderBy(f => f).Select(f => new object[] { Path.GetFileName(f) });
        }

        [Theory]
        [MemberData(nameof(CorpusFiles))]
        public void Svg_MatchesReferenceRendering(string file)
        {
            var path = Path.Combine(CORPUS, file);

            // the reference rendering
            using var reference = new SKSvg();
            reference.Load(path);
            var width = reference.Picture.CullRect.Width;
            var height = reference.Picture.CullRect.Height;
            var scale = RENDERSIZE / Math.Max(width, height);
            var pixelWidth = (int)Math.Ceiling(width * scale);
            var pixelHeight = (int)Math.Ceiling(height * scale);

            using var expected = NewSurface(pixelWidth, pixelHeight);
            expected.Canvas.Scale(scale);
            expected.Canvas.DrawPicture(reference.Picture);

            // our rendering: the SVG placed at the origin of a page at its natural size
            var image = new ImageFile { Name = "svg", FilePath = path };
            var document = SinglePage(new Image { ImageName = "svg", PosX = 0, PosY = 0 });
            document.Images.Add(image);
            var pdf = Publish(document);

            Assert.True(Math.Abs(image.Drawing.Width - width) <= 1 && Math.Abs(image.Drawing.Height - height) <= 1,
                $"{file}: size is {image.Drawing.Width}x{image.Drawing.Height}, expected {width}x{height}");

            // draw our output into the same pixel area, using its exact size (the reference
            // rounds its size to whole pixels, which would otherwise shift the drawing slightly)
            var drawing = image.Drawing;
            using var actual = NewSurface(pixelWidth, pixelHeight);
            new PdfPageRenderer(pdf, actual.Canvas, (float)(width * scale / drawing.Width), (float)(height * scale / drawing.Height), (float)drawing.Height)
                .RenderPage(pdf.Pages[0]);

            var different = Compare(expected, actual, pixelWidth, pixelHeight);
            if (different > MAXDIFFERENT)
            {
                SaveDiff(file, expected, actual);
            }

            Assert.True(different <= MAXDIFFERENT, $"{file}: {different:P2} of pixels differ from the reference rendering");
        }

        private static SKSurface NewSurface(int width, int height)
        {
            var surface = SKSurface.Create(new SKImageInfo(width, height, SKColorType.Rgba8888, SKAlphaType.Premul));
            surface.Canvas.Clear(SKColors.White);
            return surface;
        }

        private static double Compare(SKSurface expected, SKSurface actual, int width, int height)
        {
            using var a = expected.Snapshot();
            using var b = actual.Snapshot();
            using var pa = a.PeekPixels();
            using var pb = b.PeekPixels();
            var ba = pa.GetPixelSpan();
            var bb = pb.GetPixelSpan();

            var different = 0;
            for (var i = 0; i < width * height * 4; i += 4)
            {
                var diff = Math.Max(Math.Abs(ba[i] - bb[i]), Math.Max(Math.Abs(ba[i + 1] - bb[i + 1]), Math.Abs(ba[i + 2] - bb[i + 2])));
                if (diff > PIXELTOLERANCE) different++;
            }
            return different / (double)(width * height);
        }

        private static void SaveDiff(string file, SKSurface expected, SKSurface actual)
        {
            var folder = Path.Combine(AppContext.BaseDirectory, "svg-diffs");
            Directory.CreateDirectory(folder);
            var name = Path.GetFileNameWithoutExtension(file);

            void Save(SKSurface surface, string suffix)
            {
                using var image = surface.Snapshot();
                using var data = image.Encode(SKEncodedImageFormat.Png, 100);
                File.WriteAllBytes(Path.Combine(folder, $"{name}.{suffix}.png"), data.ToArray());
            }

            Save(expected, "expected");
            Save(actual, "actual");
        }
    }
}
