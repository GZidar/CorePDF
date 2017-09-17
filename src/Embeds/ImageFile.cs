using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Newtonsoft.Json;
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
        public const string FILETYPESVG = "image#2Fsvg+xml";
        public const string FILETYPEJPG = "image#2Fjpg";
        public const string FILETYPEPNG = "image#2Fpng";
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

        public override void PrepareStream(bool compress = false)
        {
            if ((ByteData == null || ByteData.Length == 0) && !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath))
            {
                using (var fileStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                {
                    byte[] rgbbuf = null;
                    var hasAlpha = false; 

                    if (Type == FILETYPEPNG)
                    {
                        byte[] imageData = null;
                        //compress = true;

                        using (BinaryReader br = new BinaryReader(fileStream))
                        {
                            // check the signature
                            var sig = br.ReadBytes(8);

                            if (!(sig[0] == 137 && sig[1] == 80 && sig[2] == 78 && sig[3] == 71 && sig[4] == 13 && sig[5] == 10 && sig[6] == 26 && sig[7] == 10))
                            {
                                throw new Exception(string.Format("image file {0} is not a PNG", FilePath));
                            }

                            do
                            {
                                var bytes = br.ReadBytes(4);
                                if (BitConverter.IsLittleEndian)
                                {
                                    Array.Reverse(bytes);
                                }

                                var chunkLength = BitConverter.ToInt32(bytes, 0);
                                var chunkType = Encoding.UTF8.GetString(br.ReadBytes(4));

                                if (chunkLength > 0)
                                {
                                    if (chunkType == "IDAT")
                                    {
                                        // image data
                                        if (imageData != null)
                                        {
                                            var chunkData = br.ReadBytes(chunkLength);
                                            var newArray = new byte[imageData.Length + chunkLength];

                                            Array.Copy(imageData, newArray, imageData.Length);
                                            Array.Copy(chunkData, 0, newArray, imageData.Length, chunkLength);
                                            imageData = newArray;
                                        }
                                        else
                                        {
                                            imageData = br.ReadBytes(chunkLength);
                                        }
                                    }
                                    else
                                    {
                                        var chunkData = br.ReadBytes(chunkLength);
                                        if (chunkType == "IHDR")
                                        {
                                            var width = new byte[4];
                                            var height = new byte[4];

                                            // Can get details of the PNG here
                                            Array.Copy(chunkData, 0, width, 0, 4);
                                            Array.Copy(chunkData, 4, height, 0, 4);

                                            if (BitConverter.IsLittleEndian)
                                            {
                                                Array.Reverse(width);
                                                Array.Reverse(height);
                                            }

                                            Width = BitConverter.ToInt32(width, 0);
                                            Height = BitConverter.ToInt32(height, 0);

                                            hasAlpha = (chunkData[9] == 4 || chunkData[9] == 6);

                                            _bitsPerComponent = chunkData[8];
                                        }
                                    }
                                }

                                var chunkCRC = br.ReadBytes(4);

                            } while (fileStream.Position < fileStream.Length);
                        }

                        // Now decompress the image data array
                        using (var memoryStream = new MemoryStream(imageData))
                        {
                            using (var unzipStream = new InflaterInputStream(memoryStream))
                            {
                                using (var cmpStream = new MemoryStream())
                                {
                                    unzipStream.CopyTo(cmpStream);
                                    rgbbuf = cmpStream.ToArray();
                                }
                            }
                        }

                        if (hasAlpha)
                        {
                            // PNGs have transparancy so these need to be split between rbg and alpha channel data
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

                                //if (BitConverter.IsLittleEndian)
                                //{
                                //    Array.Reverse(bytes);
                                //}

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

                            MaskData.ByteData = new byte[a];
                            MaskData.Height = Height;
                            MaskData.Width = Width;

                            Array.Copy(rbuf, rgbbuf, r);
                            Array.Copy(abuf, MaskData.ByteData, a);

                            MaskData.PrepareStream(compress);
                        }
                    }
                    else if (Type == FILETYPEJPG)
                    {
                        var fileInfo = new FileInfo(FilePath);
                        rgbbuf = new byte[fileInfo.Length];

                        //add the bytes that represent the actual image data
                        if (rgbbuf.Length != fileStream.Read(rgbbuf, 0, rgbbuf.Length))
                        {
                            throw new Exception(string.Format("error occurred whilst reading image file {0}", FilePath));
                        }
                    }

                    ByteData = rgbbuf;
                }
            }

            _encodedData = ByteData;

            base.PrepareStream(compress);
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
                    if (MaskData.ByteData != null)
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
