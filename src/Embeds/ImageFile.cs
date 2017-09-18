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
        public const string FILETYPESVG = "image/svg";
        public const string FILETYPEJPG = "image/jpeg";
        public const string FILETYPEPNG = "image/png";
        public const string IMAGESMASK = "imagesmask";

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
        /// The file type being included.
        /// 
        /// ONLY JPG IS supported for now
        /// </summary>
        public string Type { get; set; } = FILETYPEJPG;

        /// <summary>
        /// The RBG data for the image. If a filename is provided then this field will
        /// be calculated upon import of the files. 
        /// </summary>
        public byte[] ByteData { get; set; }

        /// <summary>
        /// Width of the image in pixels. This is used to apply the scale factor when
        /// Placing the image on the page. Used to ensure printed image retains proper
        /// aspect ratio.
        /// </summary>
        public int Width { get; set; }

        /// <summary>
        /// Height of the image in pixels. This is used to apply the scale factor when
        /// Placing the image on the page. Used to ensure printed image retains proper
        /// aspect ratio.
        /// </summary>
        public int Height { get; set; }

        public void EmbedFile()
        {
            //if (Name == "toucan")
            //{
            //    Height = 3;
            //    Width = 3;
            //    Type = FILETYPEPNG;

            //    ByteData = new byte[9 * 3];

            //    ByteData[0] = 255;
            //    ByteData[1] = 0;
            //    ByteData[2] = 0;
            //    ByteData[3] = 0;
            //    ByteData[4] = 255;
            //    ByteData[5] = 0;
            //    ByteData[6] = 0;
            //    ByteData[7] = 0;
            //    ByteData[8] = 255;
            //    ByteData[9] = 0;
            //    ByteData[10] = 0;
            //    ByteData[11] = 0;
            //    ByteData[12] = 255;
            //    ByteData[13] = 255;
            //    ByteData[14] = 255;
            //    ByteData[15] = 128;
            //    ByteData[16] = 128;
            //    ByteData[17] = 128;
            //    ByteData[18] = 255;
            //    ByteData[19] = 0;
            //    ByteData[20] = 0;
            //    ByteData[21] = 255;
            //    ByteData[22] = 0;
            //    ByteData[23] = 0;
            //    ByteData[24] = 255;
            //    ByteData[25] = 0;
            //    ByteData[26] = 0;

            //    return;
            //}

            using (var image = Image.Load(FilePath))
            {
                Height = image.Height;
                Width = image.Width;
                Type = FILETYPEPNG;

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
                        Type = IMAGESMASK
                    };

                    Array.Copy(abuf, MaskData.ByteData, a);
                }

                Array.Copy(rbuf, rgbbuf, r);
                ByteData = rgbbuf;
            }

            //using (var fileStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
            //{
            //    byte[] rgbbuf = null;
            //    var hasAlpha = false;

            //    if (Type == FILETYPEPNG)
            //    {
            //        byte[] imageData = null;

            //        using (BinaryReader br = new BinaryReader(fileStream))
            //        {
            //            // check the signature
            //            var sig = br.ReadBytes(8);

            //            if (!(sig[0] == 137 && sig[1] == 80 && sig[2] == 78 && sig[3] == 71 && sig[4] == 13 && sig[5] == 10 && sig[6] == 26 && sig[7] == 10))
            //            {
            //                throw new Exception(string.Format("image file {0} is not a PNG", FilePath));
            //            }

            //            do
            //            {
            //                var bytes = br.ReadBytes(4);
            //                if (BitConverter.IsLittleEndian)
            //                {
            //                    Array.Reverse(bytes);
            //                }

            //                var chunkLength = BitConverter.ToInt32(bytes, 0);
            //                var chunkType = Encoding.UTF8.GetString(br.ReadBytes(4));

            //                if (chunkLength > 0)
            //                {
            //                    if (chunkType == "IDAT")
            //                    {
            //                        // image data
            //                        if (imageData != null)
            //                        {
            //                            var chunkData = br.ReadBytes(chunkLength);
            //                            var newArray = new byte[imageData.Length + chunkLength];

            //                            Array.Copy(imageData, newArray, imageData.Length);
            //                            Array.Copy(chunkData, 0, newArray, imageData.Length, chunkLength);
            //                            imageData = newArray;
            //                        }
            //                        else
            //                        {
            //                            imageData = br.ReadBytes(chunkLength);
            //                        }
            //                    }
            //                    else
            //                    {
            //                        var chunkData = br.ReadBytes(chunkLength);
            //                        if (chunkType == "IHDR")
            //                        {
            //                            var width = new byte[4];
            //                            var height = new byte[4];

            //                            // Can get details of the PNG here
            //                            Array.Copy(chunkData, 0, width, 0, 4);
            //                            Array.Copy(chunkData, 4, height, 0, 4);

            //                            if (BitConverter.IsLittleEndian)
            //                            {
            //                                Array.Reverse(width);
            //                                Array.Reverse(height);
            //                            }

            //                            Width = BitConverter.ToInt32(width, 0);
            //                            Height = BitConverter.ToInt32(height, 0);

            //                            hasAlpha = (chunkData[9] == 4 || chunkData[9] == 6);

            //                            _bitsPerComponent = chunkData[8];
            //                        }
            //                    }
            //                }

            //                var chunkCRC = br.ReadBytes(4);

            //            } while (fileStream.Position < fileStream.Length);
            //        }

            //        if (hasAlpha)
            //        {
            //            // Now decompress the image data array
            //            using (var memoryStream = new MemoryStream(imageData))
            //            {
            //                using (var unzipStream = new InflaterInputStream(memoryStream))
            //                {
            //                    using (var cmpStream = new MemoryStream())
            //                    {
            //                        unzipStream.CopyTo(cmpStream);
            //                        rgbbuf = cmpStream.ToArray();
            //                    }
            //                }
            //            }

            //            // PNGs have transparancy so these need to be split between rbg and alpha channel data
            //            var abuf = new byte[rgbbuf.Length];
            //            var rbuf = new byte[rgbbuf.Length];

            //            var i = 0;
            //            var a = 0;
            //            var r = 0;
            //            while (i < rgbbuf.Length)
            //            {
            //                var bytes = new byte[4];
            //                bytes[0] = rgbbuf[i];
            //                i++;
            //                bytes[1] = rgbbuf[i];
            //                i++;
            //                bytes[2] = rgbbuf[i];
            //                i++;
            //                bytes[3] = rgbbuf[i];
            //                i++;

            //                //if (BitConverter.IsLittleEndian)
            //                //{
            //                //    Array.Reverse(bytes);
            //                //}

            //                rbuf[r] = bytes[0];
            //                r++;
            //                rbuf[r] = bytes[1];
            //                r++;
            //                rbuf[r] = bytes[2];
            //                r++;
            //                abuf[a] = bytes[3];
            //                a++;
            //            }

            //            rgbbuf = new byte[r];

            //            MaskData = new ImageFile()
            //            {
            //                ByteData = new byte[a],
            //                Height = Height,
            //                Width = Width,
            //                Type = IMAGESMASK
            //            };

            //            Array.Copy(rbuf, rgbbuf, r);
            //            Array.Copy(abuf, MaskData.ByteData, a);
            //        }
            //        else
            //        {
            //            // just keep the already compressed data
            //            _compressed = true;
            //            rgbbuf = imageData;
            //        }
            //    }
            //    else if (Type == FILETYPEJPG)
            //    {
            //        var fileInfo = new FileInfo(FilePath);
            //        rgbbuf = new byte[fileInfo.Length];

            //        //add the bytes that represent the actual image data
            //        if (rgbbuf.Length != fileStream.Read(rgbbuf, 0, rgbbuf.Length))
            //        {
            //            throw new Exception(string.Format("error occurred whilst reading image file {0}", FilePath));
            //        }
            //    }

            //    ByteData = rgbbuf;
            //}
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
                case FILETYPEJPG:
                    PDFData.Add("/ColorSpace", "/DeviceRGB");
                    PDFData.Add("/Filter", "/DCTDecode");
                    break;

                case FILETYPEPNG:
                    PDFData.Add("/ColorSpace", "/DeviceRGB");
                    PDFData.Add("/Interpolate", "true");
                    if (MaskData != null)
                    {
                        PDFData.Add("/SMask", string.Format("{0} 0 R", MaskData.ObjectNumber));
                    }
                    break;

                case IMAGESMASK:
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
