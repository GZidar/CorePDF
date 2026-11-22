using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;

namespace CorePDF.UnitTests
{
    /// <summary>
    /// Minimal encoders used to generate test images covering the variations of each format
    /// (colour types, bit depths, filters, interlacing, transparency, etc).
    /// </summary>
    internal static class TestImageEncoders
    {
        #region PNG

        private static readonly int[,] Adam7 =
        {
            { 0, 0, 8, 8 }, { 4, 0, 8, 8 }, { 0, 4, 4, 8 }, { 2, 0, 4, 4 }, { 0, 2, 2, 4 }, { 1, 0, 2, 2 }, { 0, 1, 1, 2 }
        };

        public static int PngChannels(int colorType) => colorType switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => throw new ArgumentException() };

        /// <summary>
        /// Encodes a PNG. sample(x, y, channel) returns the raw value at the image's bit depth.
        /// Each row uses a different filter type so all five are exercised.
        /// </summary>
        public static byte[] Png(int width, int height, int colorType, int bitDepth, Func<int, int, int, int> sample,
            bool interlace = false, byte[] palette = null, byte[] transparency = null)
        {
            var channels = PngChannels(colorType);
            var bitsPerPixel = channels * bitDepth;
            var bpp = Math.Max(1, bitsPerPixel / 8);
            var raw = new MemoryStream();
            var filterType = 0;

            for (var pass = 0; pass < (interlace ? 7 : 1); pass++)
            {
                int x0 = 0, y0 = 0, dx = 1, dy = 1;
                if (interlace)
                {
                    x0 = Adam7[pass, 0]; y0 = Adam7[pass, 1]; dx = Adam7[pass, 2]; dy = Adam7[pass, 3];
                }
                var passWidth = (width - x0 + dx - 1) / dx;
                var passHeight = (height - y0 + dy - 1) / dy;
                if (passWidth <= 0 || passHeight <= 0) continue;

                var rowBytes = ((passWidth * bitsPerPixel) + 7) / 8;
                var previous = new byte[rowBytes];

                for (var row = 0; row < passHeight; row++)
                {
                    var line = new byte[rowBytes];
                    for (var col = 0; col < passWidth; col++)
                    {
                        for (var c = 0; c < channels; c++)
                        {
                            Pack(line, col, c, channels, bitDepth, sample(x0 + (col * dx), y0 + (row * dy), c));
                        }
                    }

                    var filter = (byte)(filterType++ % 5);
                    raw.WriteByte(filter);
                    for (var i = 0; i < rowBytes; i++)
                    {
                        int a = i >= bpp ? line[i - bpp] : 0, b = previous[i], cc = i >= bpp ? previous[i - bpp] : 0;
                        var predictor = filter switch
                        {
                            1 => a,
                            2 => b,
                            3 => (a + b) / 2,
                            4 => Paeth(a, b, cc),
                            _ => 0
                        };
                        raw.WriteByte((byte)(line[i] - predictor));
                    }
                    previous = line;
                }
            }

            var output = new MemoryStream();
            output.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

            var ihdr = new byte[13];
            WriteBE32(ihdr, 0, width);
            WriteBE32(ihdr, 4, height);
            ihdr[8] = (byte)bitDepth;
            ihdr[9] = (byte)colorType;
            ihdr[12] = (byte)(interlace ? 1 : 0);
            Chunk(output, "IHDR", ihdr);
            if (palette != null) Chunk(output, "PLTE", palette);
            if (transparency != null) Chunk(output, "tRNS", transparency);

            var compressed = new MemoryStream();
            using (var z = new ZLibStream(compressed, CompressionLevel.Optimal, true))
            {
                raw.Position = 0;
                raw.CopyTo(z);
            }

            // split the image data over two chunks, as decoders must join them
            var data = compressed.ToArray();
            var half = data.Length / 2;
            Chunk(output, "IDAT", data.AsSpan(0, half).ToArray());
            Chunk(output, "IDAT", data.AsSpan(half).ToArray());
            Chunk(output, "IEND", new byte[0]);
            return output.ToArray();
        }

        private static void Pack(byte[] line, int col, int channel, int channels, int bitDepth, int value)
        {
            if (bitDepth == 8)
            {
                line[(col * channels) + channel] = (byte)value;
            }
            else if (bitDepth == 16)
            {
                var pos = ((col * channels) + channel) * 2;
                line[pos] = (byte)(value >> 8);
                line[pos + 1] = (byte)value;
            }
            else
            {
                var bit = col * bitDepth;
                line[bit / 8] |= (byte)(value << (8 - bitDepth - (bit % 8)));
            }
        }

        private static int Paeth(int a, int b, int c)
        {
            var p = a + b - c;
            int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
            return pa <= pb && pa <= pc ? a : pb <= pc ? b : c;
        }

        private static void Chunk(Stream output, string type, byte[] data)
        {
            var header = new byte[8];
            WriteBE32(header, 0, data.Length);
            Encoding.ASCII.GetBytes(type).CopyTo(header, 4);
            output.Write(header);
            output.Write(data);

            var crcData = new byte[4 + data.Length];
            Encoding.ASCII.GetBytes(type).CopyTo(crcData, 0);
            data.CopyTo(crcData, 4);
            var crc = new byte[4];
            WriteBE32(crc, 0, (int)Crc32(crcData));
            output.Write(crc);
        }

        private static uint Crc32(byte[] data)
        {
            var crc = 0xFFFFFFFFu;
            foreach (var b in data)
            {
                crc ^= b;
                for (var k = 0; k < 8; k++)
                {
                    crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
                }
            }
            return ~crc;
        }

        private static void WriteBE32(byte[] data, int pos, int value)
        {
            data[pos] = (byte)(value >> 24);
            data[pos + 1] = (byte)(value >> 16);
            data[pos + 2] = (byte)(value >> 8);
            data[pos + 3] = (byte)value;
        }

        #endregion

        #region GIF

        /// <summary>
        /// Encodes a single frame GIF using LZW compression with the full variable code size range
        /// </summary>
        public static byte[] Gif(int width, int height, byte[] indices, byte[] palette, int transparentIndex = -1, bool interlace = false)
        {
            var tableBits = 1;
            while ((3 << tableBits) < palette.Length) tableBits++;
            var table = new byte[3 << tableBits];
            Array.Copy(palette, table, palette.Length);

            var output = new MemoryStream();
            output.Write(Encoding.ASCII.GetBytes("GIF89a"));
            WriteLE16(output, width);
            WriteLE16(output, height);
            output.WriteByte((byte)(0x80 | (tableBits - 1)));
            output.WriteByte(0);
            output.WriteByte(0);
            output.Write(table);

            if (transparentIndex >= 0)
            {
                output.Write(new byte[] { 0x21, 0xF9, 4, 0x01, 0, 0, (byte)transparentIndex, 0 });
            }

            output.WriteByte(0x2C);
            WriteLE16(output, 0);
            WriteLE16(output, 0);
            WriteLE16(output, width);
            WriteLE16(output, height);
            output.WriteByte((byte)(interlace ? 0x40 : 0));

            // interlaced images store their rows in four passes
            var ordered = indices;
            if (interlace)
            {
                ordered = new byte[indices.Length];
                var n = 0;
                foreach (var (start, step) in new[] { (0, 8), (4, 8), (2, 4), (1, 2) })
                {
                    for (var y = start; y < height; y += step)
                    {
                        Array.Copy(indices, y * width, ordered, n * width, width);
                        n++;
                    }
                }
            }

            var minCodeSize = Math.Max(2, tableBits);
            output.WriteByte((byte)minCodeSize);
            var compressed = Lzw(ordered, minCodeSize);
            for (var i = 0; i < compressed.Length; i += 255)
            {
                var length = Math.Min(255, compressed.Length - i);
                output.WriteByte((byte)length);
                output.Write(compressed, i, length);
            }
            output.WriteByte(0);
            output.WriteByte(0x3B);
            return output.ToArray();
        }

        private static byte[] Lzw(byte[] indices, int minCodeSize)
        {
            var clear = 1 << minCodeSize;
            var end = clear + 1;
            var output = new List<byte>();
            int bitBuffer = 0, bitCount = 0;

            void Emit(int code, int size)
            {
                bitBuffer |= code << bitCount;
                bitCount += size;
                while (bitCount >= 8)
                {
                    output.Add((byte)bitBuffer);
                    bitBuffer >>= 8;
                    bitCount -= 8;
                }
            }

            var dictionary = new Dictionary<(int, byte), int>();
            var codeSize = minCodeSize + 1;
            var nextCode = clear + 2;
            Emit(clear, codeSize);

            var current = (int)indices[0];
            for (var i = 1; i < indices.Length; i++)
            {
                var c = indices[i];
                if (dictionary.TryGetValue((current, c), out var code))
                {
                    current = code;
                    continue;
                }

                Emit(current, codeSize);
                dictionary[(current, c)] = nextCode++;
                if (nextCode > (1 << codeSize) && codeSize < 12) codeSize++;

                if (nextCode >= 4093)
                {
                    // the table is full: start again
                    Emit(clear, codeSize);
                    dictionary.Clear();
                    codeSize = minCodeSize + 1;
                    nextCode = clear + 2;
                }
                current = c;
            }

            Emit(current, codeSize);
            Emit(end, codeSize);
            if (bitCount > 0) output.Add((byte)bitBuffer);
            return output.ToArray();
        }

        private static void WriteLE16(Stream output, int value)
        {
            output.WriteByte((byte)value);
            output.WriteByte((byte)(value >> 8));
        }

        #endregion

        #region BMP

        /// <summary>
        /// Encodes a bitmap. pixel(x, y) returns a palette index for 1, 4 and 8 bit images or the
        /// raw pixel value for 16 and 32 bit images; 24 bit images use 0xRRGGBB.
        /// </summary>
        public static byte[] Bmp(int width, int height, int bitCount, Func<int, int, uint> pixel, byte[] paletteRgb = null,
            uint[] masks = null, bool topDown = false, bool v4Header = false)
        {
            var headerSize = v4Header ? 108 : 40;
            var compression = masks != null ? 3 : 0;
            var paletteCount = paletteRgb != null ? paletteRgb.Length / 3 : 0;
            var masksSize = masks != null && !v4Header ? 12 : 0;
            var rowBytes = (((width * bitCount) + 31) / 32) * 4;
            var pixelOffset = 14 + headerSize + masksSize + (paletteCount * 4);

            var output = new byte[pixelOffset + (rowBytes * height)];
            output[0] = (byte)'B';
            output[1] = (byte)'M';
            WriteLE32(output, 2, output.Length);
            WriteLE32(output, 10, pixelOffset);
            WriteLE32(output, 14, headerSize);
            WriteLE32(output, 18, width);
            WriteLE32(output, 22, topDown ? -height : height);
            output[26] = 1;
            output[28] = (byte)bitCount;
            WriteLE32(output, 30, compression);
            WriteLE32(output, 46, paletteCount);

            if (masks != null)
            {
                for (var i = 0; i < masks.Length; i++) WriteLE32(output, 54 + (i * 4), (int)masks[i]);
            }

            for (var i = 0; i < paletteCount; i++)
            {
                var p = 14 + headerSize + masksSize + (i * 4);
                output[p] = paletteRgb[(i * 3) + 2];
                output[p + 1] = paletteRgb[(i * 3) + 1];
                output[p + 2] = paletteRgb[i * 3];
            }

            for (var y = 0; y < height; y++)
            {
                var rowStart = pixelOffset + ((topDown ? y : height - 1 - y) * rowBytes);
                for (var x = 0; x < width; x++)
                {
                    var value = pixel(x, y);
                    switch (bitCount)
                    {
                        case 1:
                        case 4:
                        case 8:
                            var bit = x * bitCount;
                            output[rowStart + (bit / 8)] |= (byte)(value << (8 - bitCount - (bit % 8)));
                            break;
                        case 16:
                            output[rowStart + (x * 2)] = (byte)value;
                            output[rowStart + (x * 2) + 1] = (byte)(value >> 8);
                            break;
                        case 24:
                            output[rowStart + (x * 3)] = (byte)value;
                            output[rowStart + (x * 3) + 1] = (byte)(value >> 8);
                            output[rowStart + (x * 3) + 2] = (byte)(value >> 16);
                            break;
                        case 32:
                            WriteLE32(output, rowStart + (x * 4), (int)value);
                            break;
                    }
                }
            }

            return output;
        }

        private static void WriteLE32(byte[] data, int pos, int value)
        {
            data[pos] = (byte)value;
            data[pos + 1] = (byte)(value >> 8);
            data[pos + 2] = (byte)(value >> 16);
            data[pos + 3] = (byte)(value >> 24);
        }

        #endregion
    }
}
