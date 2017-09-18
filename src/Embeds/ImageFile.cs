using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace CorePDF.Embeds
{
    /// <summary>
    /// Defines the images that are going to be included in the document.
    /// </summary>
    public class ImageFile : PDFObject
    {
        public const string IMAGEMASK = "imagemask";
        public const string IMAGEDATA = "imagedata";

        private int _bitsPerComponent { get; set; } = 8;

        [JsonIgnore]
        public ImageFile MaskData { get; set; }

        /// <summary>
        /// The name used to reference this image in the document content
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// A fully qualified path to the image file
        /// </summary>
        public string FilePath { get; set; }

        /// <summary>
        /// The file type of image object being included.
        /// </summary>
        public string Type { get; set; } = IMAGEDATA;

        /// <summary>
        /// The RBG data for the image. If a filename is provided then this field will
        /// be calculated upon import of the files. 
        /// </summary>
        public byte[] ByteData { get; set; }

        /// <summary>
        /// Width of the image in pixels. This is used to apply the scale factor when
        /// Placing the image on the page. Used to ensure printed image retains proper
        /// aspect ratio.
        /// This field is calculated when the image file is embedded.
        /// </summary>
        [JsonIgnore]
        public int Width { get; set; }

        /// <summary>
        /// Height of the image in pixels. This is used to apply the scale factor when
        /// Placing the image on the page. Used to ensure printed image retains proper
        /// aspect ratio.
        /// This field is calculated when the image file is embedded.
        /// </summary>
        [JsonIgnore] 
        public int Height { get; set; }

        public void EmbedFile()
        {
            using (var image = Image.Load(FilePath))
            {
                Height = image.Height;
                Width = image.Width;

                var hasAlpha = false;

                var rgbbuf = image.SavePixelData();

                var abuf = new byte[rgbbuf.Length];
                var rbuf = new byte[rgbbuf.Length];

                var i = 0;
                var a = 0;
                var r = 0;
                while (i < rgbbuf.Length)
                {
                    var bytes = new byte[4];
                    bytes[0] = rgbbuf[i];
                    i++;
                    bytes[1] = rgbbuf[i];
                    i++;
                    bytes[2] = rgbbuf[i];
                    i++;
                    bytes[3] = rgbbuf[i];
                    i++;

                    if (bytes[3] < 255)
                    {
                        hasAlpha = true;
                    }

                    rbuf[r] = bytes[0];
                    r++;
                    rbuf[r] = bytes[1];
                    r++;
                    rbuf[r] = bytes[2];
                    r++;
                    abuf[a] = bytes[3];
                    a++;
                }

                rgbbuf = new byte[r];

                if (hasAlpha)
                {
                    MaskData = new ImageFile()
                    {
                        ByteData = new byte[a],
                        Height = Height,
                        Width = Width,
                        Type = IMAGEMASK
                    };

                    Array.Copy(abuf, MaskData.ByteData, a);
                }

                Array.Copy(rbuf, rgbbuf, r);
                ByteData = rgbbuf;
            }

        }

        public override void PrepareStream(bool compress = false)
        {
            _encodedData = ByteData;

            base.PrepareStream(compress);

            if (MaskData != null)
            {
                MaskData.PrepareStream(compress);
            }
        }

        public override void Publish(StreamWriter stream)
        {
            var PDFData = new Dictionary<string, dynamic>
            {
                { "/Type", "/XObject" },
                { "/Subtype", "/Image"},
                { "/Name", "/" + Id},
                { "/Width", Width.ToString()},
                { "/Height", Height.ToString()},
                { "/BitsPerComponent", _bitsPerComponent.ToString()}
            };

            switch (Type)
            {
                case IMAGEDATA:
                    PDFData.Add("/ColorSpace", "/DeviceRGB");
                    PDFData.Add("/Interpolate", "true");
                    if (MaskData != null)
                    {
                        PDFData.Add("/SMask", string.Format("{0} 0 R", MaskData.ObjectNumber));
                    }
                    break;

                case IMAGEMASK:
                    PDFData.Add("/ColorSpace", "/DeviceGray");
                    break;
            }

            _pdfObject = PDFData;

            base.Publish(stream);

            if (MaskData != null)
            {
                // if there is an associated mask then publish that as well
                MaskData.Publish(stream);
            }
        }
    }

}
