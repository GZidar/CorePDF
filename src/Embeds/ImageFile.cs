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
        /// ONLY JPG IS Supported for now
        /// </summary>
        public string Type { get; set; } = FILETYPEJPG;

        /// <summary>
        /// The RBG data for the image. If a filename is provided then this field will
        /// be calculated upon import of the files. 
        /// </summary>
        public byte[] ByteData { get; set; }

        /// <summary>
        /// The Alpha mask of the image. Used for images that support transparancy
        /// </summary>
        public byte[] MaskData { get; set; }

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
                    var fileInfo = new FileInfo(FilePath);
                    var rgbbuf = new byte[fileInfo.Length];

                    //add the bytes that represent the actual image data
                    if (rgbbuf.Length != fileStream.Read(rgbbuf, 0, rgbbuf.Length))
                    {
                        throw new Exception(string.Format("error occurred whilst reading image file ", FilePath));
                    }

                    if (Type == FILETYPEPNG)
                    {
                        throw new NotImplementedException();

                        //// PNGs have transparancy so these need to be split between rbg and alpha channel data
                        //var abuf = new byte[rgbbuf.Length];
                        //var rbuf = new byte[rgbbuf.Length];

                        //var i = 0;
                        //var a = 0;
                        //var r = 0;
                        //while (i < rgbbuf.Length)
                        //{
                        //    rbuf[r] = rgbbuf[i];
                        //    i++;
                        //    r++;
                        //    rbuf[r] = rgbbuf[i];
                        //    i++;
                        //    r++;
                        //    rbuf[r] = rgbbuf[i];
                        //    i++;
                        //    r++;
                        //    abuf[a] = rgbbuf[i];
                        //    i++;
                        //    a++;
                        //}

                        //Array.Copy(rbuf, rgbbuf, r);
                        //Array.Copy(abuf, file.MaskData, a);
                    }

                    ByteData = rgbbuf;
                }
            }

            _encodedData = ByteData;
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
                { "/BitsPerComponent", "8"},
                { "/ColorSpace", "/DeviceRGB" },
                { "/Filter", "/DCTDecode" }
            };

            _pdfObject = PDFData;

            base.Publish(stream);
        }
    }

}
