using System;
using System.IO;

namespace CorePDF.Imaging
{
    /// <summary>
    /// PDF readers can display JPEG data directly (the DCTDecode filter), so JPEG files don't
    /// need decoding: only the image size and number of colour components are read from them.
    /// This also avoids the loss of quality and increase in size of re-encoding the image.
    /// </summary>
    internal static class JpegReader
    {
        public static bool IsJpeg(byte[] data)
        {
            return data.Length > 3 && data[0] == 0xFF && data[1] == 0xD8 && data[2] == 0xFF;
        }

        public static DecodedImage Read(byte[] data)
        {
            var pos = 2;
            var adobe = false;

            while (pos < data.Length)
            {
                if (data[pos] != 0xFF)
                {
                    throw new InvalidDataException("The JPEG data is corrupt.");
                }

                // markers may be preceded by any number of fill bytes
                while (pos < data.Length && data[pos] == 0xFF) pos++;
                var marker = data[pos++];

                // markers without a length
                if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD8)) continue;
                if (marker == 0xD9 || marker == 0xDA)
                {
                    break; // end of image or start of the image data, without having found the frame header
                }

                var length = (data[pos] << 8) | data[pos + 1];

                if (marker == 0xEE && length >= 14 && data[pos + 2] == 'A' && data[pos + 3] == 'd' && data[pos + 4] == 'o' && data[pos + 5] == 'b' && data[pos + 6] == 'e')
                {
                    adobe = true;
                }

                // start of frame markers (C4, C8 and CC are other tables)
                if (marker >= 0xC0 && marker <= 0xCF && marker != 0xC4 && marker != 0xC8 && marker != 0xCC)
                {
                    var precision = data[pos + 2];
                    var height = (data[pos + 3] << 8) | data[pos + 4];
                    var width = (data[pos + 5] << 8) | data[pos + 6];
                    var components = data[pos + 7];

                    if (marker == 0xC3 || marker == 0xC7 || marker == 0xCB || marker == 0xCF)
                    {
                        throw new NotSupportedException("Lossless JPEG images are not supported.");
                    }
                    if (marker >= 0xC9)
                    {
                        throw new NotSupportedException("JPEG images using arithmetic coding are not supported.");
                    }
                    if (precision != 8)
                    {
                        throw new NotSupportedException($"{precision} bit JPEG images are not supported.");
                    }
                    if (width == 0 || height == 0)
                    {
                        throw new NotSupportedException("JPEG images that don't record their height in the frame header are not supported.");
                    }

                    string colorSpace;
                    switch (components)
                    {
                        case 1: colorSpace = "DeviceGray"; break;
                        case 3: colorSpace = "DeviceRGB"; break;
                        case 4: colorSpace = "DeviceCMYK"; break;
                        default: throw new NotSupportedException($"JPEG images with {components} colour components are not supported.");
                    }

                    // keep scanning for an Adobe marker, which may follow the frame header
                    var result = new DecodedImage
                    {
                        Width = width,
                        Height = height,
                        ColorSpace = colorSpace,
                        Data = data,
                        IsJpeg = true
                    };

                    adobe |= HasAdobeMarker(data, pos + length);
                    if (components == 4 && adobe)
                    {
                        // Adobe applications store CMYK JPEGs with inverted values
                        result.Decode = "[1 0 1 0 1 0 1 0]";
                    }

                    return result;
                }

                pos += length;
            }

            throw new InvalidDataException("The JPEG data does not contain a frame header.");
        }

        private static bool HasAdobeMarker(byte[] data, int pos)
        {
            while (pos + 4 < data.Length && data[pos] == 0xFF)
            {
                while (pos < data.Length && data[pos] == 0xFF) pos++;
                var marker = data[pos++];
                if (marker == 0xDA || marker == 0xD9) return false;
                if (marker == 0x01 || (marker >= 0xD0 && marker <= 0xD8)) continue;

                var length = (data[pos] << 8) | data[pos + 1];
                if (marker == 0xEE && length >= 7 && pos + 7 <= data.Length
                    && data[pos + 2] == 'A' && data[pos + 3] == 'd' && data[pos + 4] == 'o' && data[pos + 5] == 'b' && data[pos + 6] == 'e')
                {
                    return true;
                }
                pos += length;
            }
            return false;
        }
    }
}
