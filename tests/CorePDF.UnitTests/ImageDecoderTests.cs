using CorePDF.Imaging;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Xunit;
using static CorePDF.UnitTests.TestImageEncoders;

namespace CorePDF.UnitTests
{
    /// <summary>
    /// Compares the library's image decoders with SkiaSharp (used here only as a reference,
    /// the library itself has no dependency on it) across the variations of each format.
    /// </summary>
    [Trait("Category", "Unit Test")]
    public class ImageDecoderTests
    {
        /// <summary>
        /// A repeatable "random" value so the images contain a mix of every sample value
        /// </summary>
        private static int Noise(int x, int y, int c, int max)
        {
            unchecked
            {
                var h = (uint)((x * 73856093) ^ (y * 19349663) ^ (c * 83492791) ^ 0x5bd1e995);
                h ^= h >> 13;
                h *= 0x5bd1e995;
                h ^= h >> 15;
                return (int)(h % (uint)(max + 1));
            }
        }

        private static byte[] Palette(int entries)
        {
            var result = new byte[entries * 3];
            for (var i = 0; i < result.Length; i++) result[i] = (byte)Noise(i, 7, 1, 255);
            return result;
        }

        #region Comparison

        private static (int Width, int Height, byte[] Rgba) Skia(byte[] data)
        {
            using var codec = SKCodec.Create(new MemoryStream(data));
            Assert.NotNull(codec);
            var info = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Rgba8888, SKAlphaType.Unpremul);
            Assert.Equal(SKCodecResult.Success, codec.GetPixels(info, out byte[] pixels));
            return (info.Width, info.Height, pixels);
        }

        private static (int Width, int Height, byte[] Rgba) Ours(byte[] data)
        {
            var image = ImageDecoder.Decode(data);
            var count = image.Width * image.Height;
            var channels = image.ColorSpace == "DeviceGray" ? 1 : 3;
            Assert.Equal(count * channels, image.Data.Length);

            var rgba = new byte[count * 4];
            for (var p = 0; p < count; p++)
            {
                for (var c = 0; c < 3; c++)
                {
                    rgba[(p * 4) + c] = image.Data[(p * channels) + (channels == 1 ? 0 : c)];
                }
                rgba[(p * 4) + 3] = image.Alpha?[p] ?? 255;
            }
            return (image.Width, image.Height, rgba);
        }

        /// <summary>
        /// Checks both decoders give the same pixels. Colours of fully transparent pixels aren't
        /// compared as they aren't visible.
        /// </summary>
        private static void AssertMatchesSkia(byte[] data, int tolerance = 0)
        {
            var expected = Skia(data);
            var actual = Ours(data);

            Assert.Equal(expected.Width, actual.Width);
            Assert.Equal(expected.Height, actual.Height);

            for (var p = 0; p < expected.Width * expected.Height; p++)
            {
                var i = p * 4;
                var alpha = expected.Rgba[i + 3];
                Assert.True(Math.Abs(alpha - actual.Rgba[i + 3]) <= tolerance, $"alpha differs at pixel {p}: expected {alpha}, got {actual.Rgba[i + 3]}");
                if (alpha == 0) continue;

                for (var c = 0; c < 3; c++)
                {
                    Assert.True(Math.Abs(expected.Rgba[i + c] - actual.Rgba[i + c]) <= tolerance,
                        $"channel {c} differs at pixel {p} ({p % expected.Width},{p / expected.Width}): expected {expected.Rgba[i + c]}, got {actual.Rgba[i + c]}");
                }
            }
        }

        #endregion

        [Theory]
        [InlineData("sample.png")]
        [InlineData("square.png")]
        [InlineData("rpng2-bg16-toucan.png")]
        [InlineData("square.gif")]
        public void TestAssets_MatchSkia(string file)
        {
            AssertMatchesSkia(File.ReadAllBytes(file));
        }

        public static IEnumerable<object[]> PngVariants()
        {
            var combinations = new[] { (0, 1), (0, 2), (0, 4), (0, 8), (0, 16), (2, 8), (2, 16), (3, 1), (3, 2), (3, 4), (3, 8), (4, 8), (4, 16), (6, 8), (6, 16) };
            foreach (var (colorType, bitDepth) in combinations)
            {
                foreach (var interlace in new[] { false, true })
                {
                    yield return new object[] { colorType, bitDepth, interlace, false };
                    if (colorType == 0 || colorType == 2 || colorType == 3)
                    {
                        yield return new object[] { colorType, bitDepth, interlace, true };
                    }
                }
            }
        }

        [Theory]
        [MemberData(nameof(PngVariants))]
        public void Png_AllColourTypesAndBitDepths_MatchSkia(int colorType, int bitDepth, bool interlace, bool transparency)
        {
            const int width = 13, height = 11; // odd sizes leave partial bytes and small interlace passes
            var max = (1 << bitDepth) - 1;
            byte[] palette = null, trns = null;

            if (colorType == 3)
            {
                palette = Palette(1 << bitDepth);
                if (transparency) trns = new byte[] { 0, 128, 255 }.Take(1 << bitDepth).ToArray();
            }
            else if (transparency)
            {
                // make a colour that appears in the image transparent
                var hit = Enumerable.Range(0, PngChannels(colorType)).Select(c => Noise(3, 4, c, max)).ToArray();
                trns = hit.SelectMany(v => new[] { (byte)(v >> 8), (byte)v }).ToArray();
            }

            var png = Png(width, height, colorType, bitDepth, (x, y, c) => Noise(x, y, c, max), interlace, palette, trns);

            // 16 bit samples may be reduced to 8 bits with slightly different rounding
            AssertMatchesSkia(png, bitDepth == 16 ? 1 : 0);
        }

        [Fact]
        public void Png_Opaque_HasNoAlphaChannel()
        {
            var png = Png(4, 4, 6, 8, (x, y, c) => c == 3 ? 255 : x * 10);

            Assert.Null(ImageDecoder.Decode(png).Alpha);
        }

        [Fact]
        public void Png_Grey_IsWrittenAsGrey()
        {
            var image = ImageDecoder.Decode(Png(4, 4, 0, 8, (x, y, c) => x * 10));

            Assert.Equal("DeviceGray", image.ColorSpace);
            Assert.Equal(16, image.Data.Length);
        }

        [Theory]
        [InlineData(false, 2)]
        [InlineData(true, 2)]
        [InlineData(false, 4)]
        [InlineData(false, 16)]
        [InlineData(true, 256)]
        public void Gif_MatchesSkia(bool interlace, int colours)
        {
            const int width = 61, height = 67; // enough pixels to fill the LZW table and restart it
            var indices = new byte[width * height];
            for (var i = 0; i < indices.Length; i++) indices[i] = (byte)Noise(i % width, i / width, 0, colours - 1);

            AssertMatchesSkia(Gif(width, height, indices, Palette(colours), interlace: interlace));
        }

        [Fact]
        public void Gif_RepeatingPattern_MatchesSkia()
        {
            // long runs of the same values exercise LZW codes that refer to themselves
            var indices = Enumerable.Range(0, 40 * 40).Select(i => (byte)((i / 37) % 3)).ToArray();

            AssertMatchesSkia(Gif(40, 40, indices, Palette(4)));
        }

        [Fact]
        public void Gif_TransparentIndex_MatchesSkia()
        {
            var indices = Enumerable.Range(0, 20 * 20).Select(i => (byte)(i % 4)).ToArray();

            var gif = Gif(20, 20, indices, Palette(4), transparentIndex: 2);

            AssertMatchesSkia(gif);
            Assert.NotNull(ImageDecoder.Decode(gif).Alpha);
        }

        public static IEnumerable<object[]> BmpVariants()
        {
            yield return new object[] { 1, null, false, false };
            yield return new object[] { 4, null, false, false };
            yield return new object[] { 8, null, false, false };
            yield return new object[] { 8, null, true, false };
            yield return new object[] { 24, null, false, false };
            yield return new object[] { 24, null, true, false };
            yield return new object[] { 16, null, false, false }; // 5-5-5
            yield return new object[] { 16, new uint[] { 0xF800, 0x07E0, 0x001F }, false, false }; // 5-6-5
            yield return new object[] { 32, new uint[] { 0x00FF0000, 0x0000FF00, 0x000000FF }, false, false };
            yield return new object[] { 32, new uint[] { 0x00FF0000, 0x0000FF00, 0x000000FF, 0xFF000000 }, false, true };
        }

        [Theory]
        [MemberData(nameof(BmpVariants))]
        public void Bmp_MatchesSkia(int bitCount, uint[] masks, bool topDown, bool v4Header)
        {
            const int width = 13, height = 9;
            var palette = bitCount <= 8 ? Palette(1 << bitCount) : null;

            Func<int, int, uint> pixel = bitCount switch
            {
                <= 8 => (x, y) => (uint)Noise(x, y, 0, (1 << bitCount) - 1),
                16 => (x, y) => (uint)Noise(x, y, 0, 0x7FFF),
                24 => (x, y) => (uint)Noise(x, y, 0, 0xFFFFFF),
                _ => (x, y) => (uint)Noise(x, y, 0, 0xFFFFFF) | (v4Header ? (uint)Noise(x, y, 1, 255) << 24 : 0xFF000000u)
            };

            AssertMatchesSkia(Bmp(width, height, bitCount, pixel, palette, masks, topDown, v4Header));
        }

        [Fact]
        public void Bmp_32BitWithoutAlphaMask_IsOpaque()
        {
            // the fourth byte of an uncompressed 32 bit pixel is unused, so must not be treated as alpha
            var bmp = Bmp(4, 4, 32, (x, y) => 0x00123456);

            var image = ImageDecoder.Decode(bmp);

            Assert.Null(image.Alpha);
            Assert.Equal(new byte[] { 0x12, 0x34, 0x56 }, image.Data.Take(3));
        }

        [Theory]
        [InlineData("smiley.jpg", "DeviceRGB")]
        [InlineData("square.jpg", "DeviceRGB")]
        [InlineData("mapimage.jpeg", "DeviceRGB")]
        public void Jpeg_IsPassedThroughWithItsSize(string file, string colorSpace)
        {
            var data = File.ReadAllBytes(file);
            var expected = Skia(data);

            var image = ImageDecoder.Decode(data);

            Assert.True(image.IsJpeg);
            Assert.Same(data, image.Data);
            Assert.Equal(expected.Width, image.Width);
            Assert.Equal(expected.Height, image.Height);
            Assert.Equal(colorSpace, image.ColorSpace);
        }

        [Fact]
        public void Jpeg_Greyscale_IsWrittenAsGrey()
        {
            using var bitmap = new SKBitmap(new SKImageInfo(8, 6, SKColorType.Gray8));
            bitmap.Erase(new SKColor(90, 90, 90));
            var jpeg = bitmap.Encode(SKEncodedImageFormat.Jpeg, 90).ToArray();

            var image = ImageDecoder.Decode(jpeg);

            Assert.Equal("DeviceGray", image.ColorSpace);
            Assert.Equal(8, image.Width);
            Assert.Equal(6, image.Height);
        }

        [Fact]
        public void UnsupportedFormat_ThrowsHelpfulError()
        {
            var webp = new byte[] { (byte)'R', (byte)'I', (byte)'F', (byte)'F', 0, 0, 0, 0, (byte)'W', (byte)'E', (byte)'B', (byte)'P' };

            var ex = Assert.Throws<NotSupportedException>(() => ImageDecoder.Decode(webp));
            Assert.Contains("JPEG, PNG, GIF, BMP and SVG", ex.Message);
        }

        [Fact]
        public void TruncatedPng_ThrowsInvalidData()
        {
            var png = File.ReadAllBytes("sample.png");

            Assert.ThrowsAny<InvalidDataException>(() => ImageDecoder.Decode(png.Take(png.Length / 2).ToArray()));
        }
    }
}
