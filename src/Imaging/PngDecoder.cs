using System;
using System.IO;
using System.IO.Compression;

namespace CorePDF.Imaging
{
    /// <summary>
    /// Decodes PNG images: all colour types and bit depths, transparency (tRNS) and interlacing.
    /// 16 bit samples are reduced to 8 bits. Colour management chunks (gAMA, iCCP, etc) are ignored.
    /// </summary>
    internal static class PngDecoder
    {
        private static readonly byte[] Signature = { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A };

        // Adam7 interlacing passes: starting column, starting row, column step, row step
        private static readonly int[,] Passes =
        {
            { 0, 0, 8, 8 }, { 4, 0, 8, 8 }, { 0, 4, 4, 8 }, { 2, 0, 4, 4 }, { 0, 2, 2, 4 }, { 1, 0, 2, 2 }, { 0, 1, 1, 2 }
        };

        public static bool IsPng(byte[] data)
        {
            if (data.Length < Signature.Length) return false;
            for (var i = 0; i < Signature.Length; i++)
            {
                if (data[i] != Signature[i]) return false;
            }
            return true;
        }

        public static DecodedImage Decode(byte[] data)
        {
            int width = 0, height = 0, bitDepth = 0, colorType = -1, interlace = 0;
            byte[] palette = null;
            byte[] transparency = null;
            var compressed = new MemoryStream();

            var pos = Signature.Length;
            while (pos + 8 <= data.Length)
            {
                var length = ReadInt32(data, pos);
                var type = System.Text.Encoding.ASCII.GetString(data, pos + 4, 4);
                var chunk = pos + 8;
                if (length < 0 || chunk + length > data.Length)
                {
                    throw new InvalidDataException("The PNG data is incomplete or corrupt.");
                }

                switch (type)
                {
                    case "IHDR":
                        width = ReadInt32(data, chunk);
                        height = ReadInt32(data, chunk + 4);
                        bitDepth = data[chunk + 8];
                        colorType = data[chunk + 9];
                        interlace = data[chunk + 12];
                        break;
                    case "PLTE":
                        palette = Slice(data, chunk, length);
                        break;
                    case "tRNS":
                        transparency = Slice(data, chunk, length);
                        break;
                    case "IDAT":
                        compressed.Write(data, chunk, length);
                        break;
                }

                if (type == "IEND") break;
                pos = chunk + length + 4; // skip the CRC
            }

            int channels;
            switch (colorType)
            {
                case 0: channels = 1; break; // grey
                case 2: channels = 3; break; // RGB
                case 3: channels = 1; break; // palette
                case 4: channels = 2; break; // grey and alpha
                case 6: channels = 4; break; // RGB and alpha
                default: throw new InvalidDataException("The PNG data does not have a valid header.");
            }
            if (width <= 0 || height <= 0 || (bitDepth != 1 && bitDepth != 2 && bitDepth != 4 && bitDepth != 8 && bitDepth != 16))
            {
                throw new InvalidDataException("The PNG data does not have a valid header.");
            }
            if (colorType == 3 && palette == null)
            {
                throw new InvalidDataException("The PNG image has no palette.");
            }

            byte[] raw;
            compressed.Position = 0;
            using (var inflater = new ZLibStream(compressed, CompressionMode.Decompress))
            using (var output = new MemoryStream())
            {
                inflater.CopyTo(output);
                raw = output.ToArray();
            }

            // the output is grey (with alpha) for grey images and RGBA for everything else
            var grey = colorType == 0 || colorType == 4;
            var outChannels = grey ? 2 : 4;
            var pixels = new byte[width * height * outChannels];
            var hasAlpha = colorType == 4 || colorType == 6 || transparency != null;

            var bitsPerPixel = channels * bitDepth;
            var filterBytes = Math.Max(1, bitsPerPixel / 8);
            var rawPos = 0;

            for (var pass = 0; pass < (interlace == 1 ? 7 : 1); pass++)
            {
                int x0 = 0, y0 = 0, dx = 1, dy = 1;
                if (interlace == 1)
                {
                    x0 = Passes[pass, 0];
                    y0 = Passes[pass, 1];
                    dx = Passes[pass, 2];
                    dy = Passes[pass, 3];
                }

                var passWidth = (width - x0 + dx - 1) / dx;
                var passHeight = (height - y0 + dy - 1) / dy;
                if (passWidth <= 0 || passHeight <= 0) continue;

                var rowBytes = ((passWidth * bitsPerPixel) + 7) / 8;
                var previous = new byte[rowBytes];
                var current = new byte[rowBytes];

                for (var row = 0; row < passHeight; row++)
                {
                    if (rawPos + 1 + rowBytes > raw.Length)
                    {
                        throw new InvalidDataException("The PNG image data is incomplete.");
                    }

                    var filter = raw[rawPos];
                    Array.Copy(raw, rawPos + 1, current, 0, rowBytes);
                    rawPos += 1 + rowBytes;
                    Unfilter(filter, current, previous, filterBytes);

                    var y = y0 + (row * dy);
                    for (var col = 0; col < passWidth; col++)
                    {
                        var x = x0 + (col * dx);
                        WritePixel(pixels, ((y * width) + x) * outChannels, current, col, colorType, bitDepth, channels, palette, transparency);
                    }

                    var swap = previous;
                    previous = current;
                    current = swap;
                }
            }

            return ImageDecoder.FromPixels(width, height, pixels, outChannels, hasAlpha);
        }

        private static void Unfilter(byte filter, byte[] row, byte[] previous, int bpp)
        {
            switch (filter)
            {
                case 0: // none
                    break;
                case 1: // sub
                    for (var i = bpp; i < row.Length; i++) row[i] += row[i - bpp];
                    break;
                case 2: // up
                    for (var i = 0; i < row.Length; i++) row[i] += previous[i];
                    break;
                case 3: // average
                    for (var i = 0; i < row.Length; i++)
                    {
                        var left = i >= bpp ? row[i - bpp] : 0;
                        row[i] += (byte)((left + previous[i]) / 2);
                    }
                    break;
                case 4: // paeth
                    for (var i = 0; i < row.Length; i++)
                    {
                        var a = i >= bpp ? row[i - bpp] : 0;
                        var b = previous[i];
                        var c = i >= bpp ? previous[i - bpp] : 0;
                        var p = a + b - c;
                        var pa = Math.Abs(p - a);
                        var pb = Math.Abs(p - b);
                        var pc = Math.Abs(p - c);
                        row[i] += (byte)(pa <= pb && pa <= pc ? a : pb <= pc ? b : c);
                    }
                    break;
                default:
                    throw new InvalidDataException($"The PNG image uses an unknown filter type ({filter}).");
            }
        }

        private static void WritePixel(byte[] pixels, int offset, byte[] row, int col, int colorType, int bitDepth, int channels, byte[] palette, byte[] transparency)
        {
            switch (colorType)
            {
                case 0:
                {
                    var value = Sample(row, col, 0, channels, bitDepth);
                    pixels[offset] = To8Bit(value, bitDepth);
                    pixels[offset + 1] = transparency != null && transparency.Length >= 2 && value == ((transparency[0] << 8) | transparency[1]) ? (byte)0 : (byte)255;
                    break;
                }
                case 2:
                {
                    var r = Sample(row, col, 0, channels, bitDepth);
                    var g = Sample(row, col, 1, channels, bitDepth);
                    var b = Sample(row, col, 2, channels, bitDepth);
                    pixels[offset] = To8Bit(r, bitDepth);
                    pixels[offset + 1] = To8Bit(g, bitDepth);
                    pixels[offset + 2] = To8Bit(b, bitDepth);
                    pixels[offset + 3] = transparency != null && transparency.Length >= 6
                        && r == ((transparency[0] << 8) | transparency[1])
                        && g == ((transparency[2] << 8) | transparency[3])
                        && b == ((transparency[4] << 8) | transparency[5]) ? (byte)0 : (byte)255;
                    break;
                }
                case 3:
                {
                    var index = Sample(row, col, 0, channels, bitDepth);
                    if (index * 3 + 2 < palette.Length)
                    {
                        pixels[offset] = palette[index * 3];
                        pixels[offset + 1] = palette[(index * 3) + 1];
                        pixels[offset + 2] = palette[(index * 3) + 2];
                    }
                    pixels[offset + 3] = transparency != null && index < transparency.Length ? transparency[index] : (byte)255;
                    break;
                }
                case 4:
                    pixels[offset] = To8Bit(Sample(row, col, 0, channels, bitDepth), bitDepth);
                    pixels[offset + 1] = To8Bit(Sample(row, col, 1, channels, bitDepth), bitDepth);
                    break;
                case 6:
                    for (var i = 0; i < 4; i++)
                    {
                        pixels[offset + i] = To8Bit(Sample(row, col, i, channels, bitDepth), bitDepth);
                    }
                    break;
            }
        }

        /// <summary>
        /// Reads a sample at its original bit depth
        /// </summary>
        private static int Sample(byte[] row, int col, int channel, int channels, int bitDepth)
        {
            switch (bitDepth)
            {
                case 8:
                    return row[(col * channels) + channel];
                case 16:
                    var pos = ((col * channels) + channel) * 2;
                    return (row[pos] << 8) | row[pos + 1];
                default:
                    // packed samples (only used for single channel images), most significant bits first
                    var bit = col * bitDepth;
                    var shift = 8 - bitDepth - (bit % 8);
                    return (row[bit / 8] >> shift) & ((1 << bitDepth) - 1);
            }
        }

        private static byte To8Bit(int value, int bitDepth)
        {
            switch (bitDepth)
            {
                case 1: return (byte)(value * 255);
                case 2: return (byte)(value * 85);
                case 4: return (byte)(value * 17);
                case 16: return (byte)(value >> 8);
                default: return (byte)value;
            }
        }

        private static int ReadInt32(byte[] data, int pos)
        {
            return (data[pos] << 24) | (data[pos + 1] << 16) | (data[pos + 2] << 8) | data[pos + 3];
        }

        private static byte[] Slice(byte[] data, int start, int length)
        {
            var result = new byte[length];
            Array.Copy(data, start, result, 0, length);
            return result;
        }
    }
}
