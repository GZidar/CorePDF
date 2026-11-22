using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using CorePDF.Imaging;
using CorePDF.Svg;
using CorePDF.Writing;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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
        public const string PATHDATA = "pathdata";

        private int _bitsPerComponent { get; set; } = 8;
        private string _colorSpace = "DeviceRGB";
        private bool _jpeg;
        private string _decode;
        private SvgDrawing _drawing;
        private int _firstGroupNumber;

        [JsonIgnore, IgnoreDataMember]
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
        /// The raw contents of an image file
        /// </summary>
        public byte[] FileData { get; set; }

        /// <summary>
        /// The file type of image object being included.
        /// </summary>
        public string Type { get; set; } = IMAGEDATA;

        /// <summary>
        /// The RBG data for the image. If filedata or a filename is provided then this field will
        /// be calculated upon import of the files. 
        /// </summary>
        public byte[] ByteData { get; set; }

        /// <summary>
        /// Width of the image in pixels. This is used to apply the scale factor when
        /// Placing the image on the page. Used to ensure printed image retains proper
        /// aspect ratio.
        /// This field is calculated when the image file is embedded.
        /// </summary>
        [JsonIgnore, IgnoreDataMember]
        public int Width { get; set; }

        /// <summary>
        /// Height of the image in pixels. This is used to apply the scale factor when
        /// Placing the image on the page. Used to ensure printed image retains proper
        /// aspect ratio.
        /// This field is calculated when the image file is embedded.
        /// </summary>
        [JsonIgnore, IgnoreDataMember]
        public int Height { get; set; }

        protected Stream Open()
        {
            if (FileData == null)
            {
                return new FileStream(FilePath, FileMode.Open, FileAccess.Read);
            }

            return new MemoryStream(FileData);
        }

        public void EmbedFile()
        {
            using (new InvariantCultureScope())
            {
                EmbedFileCore();
            }
        }

        private void EmbedFileCore()
        {
            // do nothing if there is no valid file or file data specified
            if (FileData == null && (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))) return;

            // start afresh, as the image is embedded each time the document is published
            MaskData = null;
            _colorSpace = "DeviceRGB";
            _jpeg = false;
            _decode = null;
            _drawing = null;

            var data = FileData ?? File.ReadAllBytes(FilePath);
            if (ImageDecoder.IsRaster(data))
            {
                EmbedRaster(data);
                return;
            }

            if (!SvgConverter.IsSvg(data))
            {
                // neither SVG nor a recognised raster format: this reports the format isn't supported
                EmbedRaster(data);
                return;
            }

            // SVG images are converted to PDF drawing operations, so they stay sharp at any size
            _drawing = SvgConverter.Convert(data);
            Type = PATHDATA;
            Width = (int)Math.Round(_drawing.Width);
            Height = (int)Math.Round(_drawing.Height);
            ByteData = null;
        }

        private void EmbedRaster(byte[] data)
        {
            var image = ImageDecoder.Decode(data);

            Type = IMAGEDATA;
            Width = image.Width;
            Height = image.Height;
            ByteData = image.Data;
            _colorSpace = image.ColorSpace;
            _jpeg = image.IsJpeg;
            _decode = image.Decode;

            if (image.Alpha != null)
            {
                MaskData = new ImageFile
                {
                    ByteData = image.Alpha,
                    Width = Width,
                    Height = Height,
                    Type = IMAGEMASK,
                    _colorSpace = "DeviceGray"
                };
            }
        }

        public override void PrepareStream(bool compress = false)
        {
            // SVG images are written as their drawing operations, raster images as their samples
            _encodedData = Type == PATHDATA ? (_drawing == null ? null : Encoding.ASCII.GetBytes(_drawing.Content)) : ByteData;

            // image data is always compressed, except JPEG data which is already compressed
            base.PrepareStream(!_jpeg);

            if (MaskData != null)
            {
                MaskData.PrepareStream(true);
            }
        }

        /// <summary>
        /// The colour space of the image samples (DeviceRGB, DeviceGray or DeviceCMYK)
        /// </summary>
        internal string ColorSpace => _colorSpace;

        /// <summary>
        /// True if the image has been loaded and will be written to the document
        /// </summary>
        internal bool IsPublished => Type == PATHDATA ? _drawing != null : ByteData != null;

        /// <summary>
        /// The converted drawing, for SVG images
        /// </summary>
        internal SvgDrawing Drawing => _drawing;

        /// <summary>
        /// Reserves object numbers for the image and the objects that go with it (its
        /// transparency mask, or an SVG image's transparency groups). Returns the next free number.
        /// </summary>
        internal int AssignObjectNumbers(int next)
        {
            ObjectNumber = next++;
            if (MaskData != null)
            {
                MaskData.ObjectNumber = next++;
            }
            if (_drawing != null)
            {
                _firstGroupNumber = next;
                next += _drawing.Groups.Count;
            }
            return next;
        }

        /// <summary>
        /// Allows the PDF reader to smooth the image when it is scaled up. Not permitted in PDF/A documents.
        /// </summary>
        internal bool Interpolate { get; set; } = true;

        public override void Publish(StreamWriter stream)
        {
            if (!IsPublished) return;

            if (Type == PATHDATA)
            {
                PublishDrawing(stream);
                return;
            }

            // save the image data
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
                    PDFData.Add("/ColorSpace", "/" + _colorSpace);
                    if (_jpeg)
                    {
                        PDFData.Add("/Filter", "/DCTDecode");
                    }
                    if (!string.IsNullOrEmpty(_decode))
                    {
                        PDFData.Add("/Decode", _decode);
                    }
                    if (Interpolate)
                    {
                        PDFData.Add("/Interpolate", "true");
                    }
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

        /// <summary>
        /// Writes an SVG image as a form XObject (a reusable drawing), followed by any
        /// transparency groups it uses
        /// </summary>
        private void PublishDrawing(StreamWriter stream)
        {
            var resources = _drawing.Resources(i => _firstGroupNumber + i);

            _pdfObject = new Dictionary<string, dynamic>
            {
                { "/Type", "/XObject" },
                { "/Subtype", "/Form" },
                { "/BBox", string.Format("[0 0 {0} {1}]", SvgNumber.Format(_drawing.Width), SvgNumber.Format(_drawing.Height)) },
                { "/Resources", resources }
            };
            base.Publish(stream);

            for (var i = 0; i < _drawing.Groups.Count; i++)
            {
                var (content, bbox) = _drawing.Groups[i];
                new RawObject(_firstGroupNumber + i, new Dictionary<string, dynamic>
                {
                    { "/Type", "/XObject" },
                    { "/Subtype", "/Form" },
                    { "/BBox", "[" + string.Join(" ", Array.ConvertAll(bbox, SvgNumber.Format)) + "]" },
                    { "/Group", "<< /Type /Group /S /Transparency >>" },
                    { "/Resources", resources }
                }, Encoding.ASCII.GetBytes(content), compress: true).Publish(stream);
            }
        }
    }
}
