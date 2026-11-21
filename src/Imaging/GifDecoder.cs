using System;
using System.IO;

namespace CorePDF.Imaging
{
    /// <summary>
    /// Decodes the first frame of a GIF image, including transparency and interlacing. The
    /// frame is placed on the image's full (logical screen) area, which is otherwise transparent.
    /// </summary>
    internal static class GifDecoder
    {
        private const int MAXCODES = 4096;

        public static bool IsGif(byte[] data)
        {
            return data.Length >= 6 && data[0] == 'G' && data[1] == 'I' && data[2] == 'F' && data[3] == '8'
                && (data[4] == '7' || data[4] == '9') && data[5] == 'a';
        }

        public static DecodedImage Decode(byte[] data)
        {
            var screenWidth = ReadUInt16(data, 6);
            var screenHeight = ReadUInt16(data, 8);
            var flags = data[10];
            var pos = 13;

            byte[] globalTable = null;
            if ((flags & 0x80) != 0)
            {
                var size = 3 * (1 << ((flags & 0x07) + 1));
                globalTable = Slice(data, pos, size);
                pos += size;
            }

            var transparentIndex = -1;
            while (pos < data.Length)
            {
                var block = data[pos++];

                if (block == 0x21)
                {
                    // an extension: only the graphic control extension (for transparency) matters
                    var label = data[pos++];
                    if (label == 0xF9 && data[pos] >= 4 && (data[pos + 1] & 0x01) != 0)
                    {
                        transparentIndex = data[pos + 4];
                    }
                    pos = SkipSubBlocks(data, pos);
                }
                else if (block == 0x2C)
                {
                    return DecodeFrame(data, pos, screenWidth, screenHeight, globalTable, transparentIndex);
                }
                else if (block == 0x3B)
                {
                    break;
                }
                else
                {
                    throw new InvalidDataException("The GIF data is corrupt.");
                }
            }

            throw new InvalidDataException("The GIF data does not contain an image.");
        }

        private static DecodedImage DecodeFrame(byte[] data, int pos, int screenWidth, int screenHeight, byte[] colourTable, int transparentIndex)
        {
            var left = ReadUInt16(data, pos);
            var top = ReadUInt16(data, pos + 2);
            var width = ReadUInt16(data, pos + 4);
            var height = ReadUInt16(data, pos + 6);
            var flags = data[pos + 8];
            pos += 9;

            if ((flags & 0x80) != 0)
            {
                var size = 3 * (1 << ((flags & 0x07) + 1));
                colourTable = Slice(data, pos, size);
                pos += size;
            }
            if (colourTable == null)
            {
                throw new InvalidDataException("The GIF image has no colour table.");
            }

            // some files have a frame larger than the screen it is meant to be shown on
            screenWidth = Math.Max(screenWidth, left + width);
            screenHeight = Math.Max(screenHeight, top + height);

            var minCodeSize = data[pos++];
            var indices = Decompress(ReadSubBlocks(data, pos), minCodeSize, width * height);

            var interlaced = (flags & 0x40) != 0;
            var rows = RowOrder(height, interlaced);

            // RGBA pixels, transparent where the frame doesn't cover the screen
            var pixels = new byte[screenWidth * screenHeight * 4];
            var hasTransparency = width < screenWidth || height < screenHeight;

            for (var i = 0; i < height; i++)
            {
                var y = top + rows[i];
                for (var x = 0; x < width; x++)
                {
                    var index = indices[(i * width) + x];
                    var offset = ((y * screenWidth) + left + x) * 4;

                    if (index == transparentIndex || (index * 3) + 2 >= colourTable.Length)
                    {
                        hasTransparency = true;
                        continue;
                    }

                    pixels[offset] = colourTable[index * 3];
                    pixels[offset + 1] = colourTable[(index * 3) + 1];
                    pixels[offset + 2] = colourTable[(index * 3) + 2];
                    pixels[offset + 3] = 255;
                }
            }

            return ImageDecoder.FromPixels(screenWidth, screenHeight, pixels, 4, hasTransparency);
        }

        /// <summary>
        /// The image row stored in each position of the data. Interlaced images store every
        /// 8th row, then the rows in between in three further passes.
        /// </summary>
        private static int[] RowOrder(int height, bool interlaced)
        {
            var result = new int[height];
            if (!interlaced)
            {
                for (var i = 0; i < height; i++) result[i] = i;
                return result;
            }

            var n = 0;
            foreach (var (start, step) in new[] { (0, 8), (4, 8), (2, 4), (1, 2) })
            {
                for (var y = start; y < height; y += step) result[n++] = y;
            }
            return result;
        }

        /// <summary>
        /// Decodes GIF's variable code size LZW compression into colour indices
        /// </summary>
        private static byte[] Decompress(byte[] input, int minCodeSize, int pixelCount)
        {
            if (minCodeSize < 2 || minCodeSize > 11)
            {
                throw new InvalidDataException("The GIF image data is corrupt.");
            }

            var output = new byte[pixelCount];
            var outPos = 0;

            var clearCode = 1 << minCodeSize;
            var endCode = clearCode + 1;
            var prefix = new short[MAXCODES];
            var suffix = new byte[MAXCODES];
            var stack = new byte[MAXCODES + 1];

            for (var i = 0; i < clearCode; i++)
            {
                suffix[i] = (byte)i;
            }

            var codeSize = minCodeSize + 1;
            var nextCode = clearCode + 2;
            var previous = -1;
            byte first = 0;

            var bitBuffer = 0;
            var bitCount = 0;
            var inPos = 0;

            while (outPos < pixelCount)
            {
                while (bitCount < codeSize)
                {
                    if (inPos >= input.Length) return output; // data ended early: the rest stays as index 0
                    bitBuffer |= input[inPos++] << bitCount;
                    bitCount += 8;
                }

                var code = bitBuffer & ((1 << codeSize) - 1);
                bitBuffer >>= codeSize;
                bitCount -= codeSize;

                if (code == clearCode)
                {
                    codeSize = minCodeSize + 1;
                    nextCode = clearCode + 2;
                    previous = -1;
                    continue;
                }
                if (code == endCode) break;

                if (previous == -1)
                {
                    if (code >= clearCode) throw new InvalidDataException("The GIF image data is corrupt.");
                    output[outPos++] = suffix[code];
                    previous = code;
                    first = suffix[code];
                    continue;
                }

                var current = code;
                var top = 0;
                if (code >= nextCode)
                {
                    if (code > nextCode) throw new InvalidDataException("The GIF image data is corrupt.");

                    // the code being defined by this step: the previous string plus its first character
                    stack[top++] = first;
                    current = previous;
                }

                while (current >= clearCode)
                {
                    stack[top++] = suffix[current];
                    current = prefix[current];
                }
                first = suffix[current];
                stack[top++] = first;

                while (top > 0 && outPos < pixelCount)
                {
                    output[outPos++] = stack[--top];
                }

                if (nextCode < MAXCODES)
                {
                    prefix[nextCode] = (short)previous;
                    suffix[nextCode] = first;
                    nextCode++;
                    if (nextCode == (1 << codeSize) && codeSize < 12)
                    {
                        codeSize++;
                    }
                }

                previous = code;
            }

            return output;
        }

        private static int SkipSubBlocks(byte[] data, int pos)
        {
            while (data[pos] != 0)
            {
                pos += data[pos] + 1;
            }
            return pos + 1;
        }

        private static byte[] ReadSubBlocks(byte[] data, int pos)
        {
            var result = new MemoryStream();
            while (pos < data.Length && data[pos] != 0)
            {
                var length = Math.Min(data[pos], data.Length - pos - 1);
                result.Write(data, pos + 1, length);
                pos += data[pos] + 1;
            }
            return result.ToArray();
        }

        private static int ReadUInt16(byte[] data, int pos)
        {
            return data[pos] | (data[pos + 1] << 8);
        }

        private static byte[] Slice(byte[] data, int start, int length)
        {
            var result = new byte[length];
            Array.Copy(data, start, result, 0, length);
            return result;
        }
    }
}
