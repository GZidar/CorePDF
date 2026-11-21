using System;
using System.IO;

namespace CorePDF.Imaging
{
    /// <summary>
    /// An image ready to be written to the document: either raw 8 bit samples (with an optional
    /// alpha channel) or, for JPEG files, the compressed file data that PDF readers can display
    /// directly.
    /// </summary>
    internal sealed class DecodedImage
    {
        public int Width { get; set; }
        public int Height { get; set; }

        /// <summary>
        /// DeviceRGB, DeviceGray or DeviceCMYK
        /// </summary>
        public string ColorSpace { get; set; } = "DeviceRGB";

        /// <summary>
        /// The image samples (8 bits per component, rows top to bottom), or the JPEG file data
        /// if <see cref="IsJpeg"/> is set
        /// </summary>
        public byte[] Data { get; set; }

        /// <summary>
        /// One byte of opacity per pixel, or null if the image is fully opaque
        /// </summary>
        public byte[] Alpha { get; set; }

        /// <summary>
        /// True if Data holds JPEG data to be written with the DCTDecode filter
        /// </summary>
        public bool IsJpeg { get; set; }

        /// <summary>
        /// An optional PDF /Decode array (used for Adobe CMYK JPEGs, which store inverted values)
        /// </summary>
        public string Decode { get; set; }
    }

    /// <summary>
    /// Reads the raster image formats supported by the library: JPEG, PNG, GIF and BMP.
    /// </summary>
    internal static class ImageDecoder
    {
        public const string SupportedFormats = "JPEG, PNG, GIF, BMP and SVG";

        /// <summary>
        /// True if the data starts with the signature of a supported raster format
        /// </summary>
        public static bool IsRaster(byte[] data)
        {
            return JpegReader.IsJpeg(data) || PngDecoder.IsPng(data) || GifDecoder.IsGif(data) || BmpDecoder.IsBmp(data);
        }

        public static DecodedImage Decode(byte[] data)
        {
            try
            {
                if (JpegReader.IsJpeg(data)) return JpegReader.Read(data);
                if (PngDecoder.IsPng(data)) return PngDecoder.Decode(data);
                if (GifDecoder.IsGif(data)) return GifDecoder.Decode(data);
                if (BmpDecoder.IsBmp(data)) return BmpDecoder.Decode(data);
            }
            catch (Exception ex) when (ex is IndexOutOfRangeException || ex is ArgumentOutOfRangeException || ex is EndOfStreamException)
            {
                throw new InvalidDataException("The image data is incomplete or corrupt.", ex);
            }

            throw new NotSupportedException($"The image format is not supported. The supported formats are {SupportedFormats}.");
        }

        /// <summary>
        /// Splits RGBA (or grey and alpha) pixels into colour samples and an alpha channel. The
        /// last of the channels is always alpha; it is dropped if hasAlpha is false or every
        /// pixel turns out to be opaque.
        /// </summary>
        public static DecodedImage FromPixels(int width, int height, byte[] pixels, int channels, bool hasAlpha)
        {
            var colourChannels = channels - 1;
            var count = width * height;
            var colour = new byte[count * colourChannels];
            var alpha = hasAlpha ? new byte[count] : null;
            var opaque = true;

            for (int p = 0, c = 0; p < count; p++)
            {
                var src = p * channels;
                for (var i = 0; i < colourChannels; i++)
                {
                    colour[c++] = pixels[src + i];
                }

                if (hasAlpha)
                {
                    alpha[p] = pixels[src + colourChannels];
                    opaque &= alpha[p] == 255;
                }
            }

            return new DecodedImage
            {
                Width = width,
                Height = height,
                ColorSpace = colourChannels == 1 ? "DeviceGray" : "DeviceRGB",
                Data = colour,
                Alpha = opaque ? null : alpha
            };
        }
    }
}
