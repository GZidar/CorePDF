using System;
using System.IO;
using System.Numerics;

namespace CorePDF.Imaging
{
    /// <summary>
    /// Decodes uncompressed Windows bitmaps: 1, 4 and 8 bit palette images and 16, 24 and 32
    /// bit colour images (including bit field masks and alpha channels).
    /// </summary>
    internal static class BmpDecoder
    {
        private const int BI_RGB = 0;
        private const int BI_BITFIELDS = 3;
        private const int BI_ALPHABITFIELDS = 6;

        public static bool IsBmp(byte[] data)
        {
            return data.Length >= 26 && data[0] == 'B' && data[1] == 'M';
        }

        public static DecodedImage Decode(byte[] data)
        {
            var pixelOffset = ReadInt32(data, 10);
            var headerSize = ReadInt32(data, 14);

            int width, height, bitCount, compression = BI_RGB, coloursUsed = 0;
            if (headerSize == 12)
            {
                // the original OS/2 header
                width = ReadUInt16(data, 18);
                height = (short)ReadUInt16(data, 20);
                bitCount = ReadUInt16(data, 24);
            }
            else if (headerSize >= 40)
            {
                width = ReadInt32(data, 18);
                height = ReadInt32(data, 22);
                bitCount = ReadUInt16(data, 28);
                compression = ReadInt32(data, 30);
                coloursUsed = ReadInt32(data, 46);
            }
            else
            {
                throw new NotSupportedException("The bitmap header format is not supported.");
            }

            if (compression != BI_RGB && compression != BI_BITFIELDS && compression != BI_ALPHABITFIELDS)
            {
                throw new NotSupportedException("Compressed bitmaps are not supported.");
            }

            var topDown = height < 0;
            height = Math.Abs(height);
            if (width <= 0 || height == 0)
            {
                throw new InvalidDataException("The bitmap has an invalid size.");
            }

            // the masks used to extract the colour channels from 16 and 32 bit pixels
            uint redMask = 0, greenMask = 0, blueMask = 0, alphaMask = 0;
            if (bitCount == 16)
            {
                redMask = 0x7C00; greenMask = 0x03E0; blueMask = 0x001F;
            }
            else if (bitCount == 32)
            {
                redMask = 0x00FF0000; greenMask = 0x0000FF00; blueMask = 0x000000FF;
            }

            if (compression == BI_BITFIELDS || compression == BI_ALPHABITFIELDS)
            {
                // the masks directly follow the basic 40 byte header (in newer headers they are part of it)
                const int maskPos = 14 + 40;
                redMask = (uint)ReadInt32(data, maskPos);
                greenMask = (uint)ReadInt32(data, maskPos + 4);
                blueMask = (uint)ReadInt32(data, maskPos + 8);
                if (compression == BI_ALPHABITFIELDS || headerSize >= 56)
                {
                    alphaMask = (uint)ReadInt32(data, maskPos + 12);
                }
            }
            else if (headerSize >= 56 && bitCount == 32)
            {
                // version 4 and 5 headers can describe an alpha channel for uncompressed images
                alphaMask = (uint)ReadInt32(data, 14 + 52);
            }

            // palette entries are blue, green, red (and a reserved byte, except in OS/2 bitmaps)
            byte[] palette = null;
            if (bitCount <= 8)
            {
                var entrySize = headerSize == 12 ? 3 : 4;
                var count = coloursUsed > 0 ? coloursUsed : 1 << bitCount;
                var paletteStart = 14 + headerSize + (compression == BI_BITFIELDS && headerSize == 40 ? 12 : 0);
                palette = new byte[count * 3];
                for (var i = 0; i < count; i++)
                {
                    var p = paletteStart + (i * entrySize);
                    palette[i * 3] = data[p + 2];
                    palette[(i * 3) + 1] = data[p + 1];
                    palette[(i * 3) + 2] = data[p];
                }
            }
            else if (bitCount != 16 && bitCount != 24 && bitCount != 32)
            {
                throw new NotSupportedException($"{bitCount} bit bitmaps are not supported.");
            }

            var hasAlpha = alphaMask != 0;
            var pixels = new byte[width * height * 4];
            var rowBytes = (((width * bitCount) + 31) / 32) * 4;

            for (var row = 0; row < height; row++)
            {
                var y = topDown ? row : height - 1 - row;
                var rowStart = pixelOffset + (row * rowBytes);

                for (var x = 0; x < width; x++)
                {
                    var offset = ((y * width) + x) * 4;
                    pixels[offset + 3] = 255;

                    switch (bitCount)
                    {
                        case 1:
                        case 2:
                        case 4:
                        case 8:
                        {
                            var bit = x * bitCount;
                            var index = (data[rowStart + (bit / 8)] >> (8 - bitCount - (bit % 8))) & ((1 << bitCount) - 1);
                            if ((index * 3) + 2 < palette.Length)
                            {
                                pixels[offset] = palette[index * 3];
                                pixels[offset + 1] = palette[(index * 3) + 1];
                                pixels[offset + 2] = palette[(index * 3) + 2];
                            }
                            break;
                        }
                        case 24:
                        {
                            var p = rowStart + (x * 3);
                            pixels[offset] = data[p + 2];
                            pixels[offset + 1] = data[p + 1];
                            pixels[offset + 2] = data[p];
                            break;
                        }
                        case 16:
                        case 32:
                        {
                            var p = rowStart + (x * (bitCount / 8));
                            var value = bitCount == 16 ? (uint)ReadUInt16(data, p) : (uint)ReadInt32(data, p);
                            pixels[offset] = Extract(value, redMask);
                            pixels[offset + 1] = Extract(value, greenMask);
                            pixels[offset + 2] = Extract(value, blueMask);
                            if (hasAlpha) pixels[offset + 3] = Extract(value, alphaMask);
                            break;
                        }
                    }
                }
            }

            return ImageDecoder.FromPixels(width, height, pixels, 4, hasAlpha);
        }

        /// <summary>
        /// Extracts a channel using its mask and scales it to 8 bits
        /// </summary>
        private static byte Extract(uint value, uint mask)
        {
            if (mask == 0) return 0;

            var shift = BitOperations.TrailingZeroCount(mask);
            var bits = BitOperations.PopCount(mask);
            var channel = (value & mask) >> shift;
            var max = (1u << bits) - 1;

            return bits >= 8 ? (byte)(channel >> (bits - 8)) : (byte)((channel * 255 + (max / 2)) / max);
        }

        private static int ReadUInt16(byte[] data, int pos)
        {
            return data[pos] | (data[pos + 1] << 8);
        }

        private static int ReadInt32(byte[] data, int pos)
        {
            return data[pos] | (data[pos + 1] << 8) | (data[pos + 2] << 16) | (data[pos + 3] << 24);
        }
    }
}
