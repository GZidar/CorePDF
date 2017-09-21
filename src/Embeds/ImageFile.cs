using ICSharpCode.SharpZipLib.Zip.Compression.Streams;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Xml.XPath;

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

        /// <summary>
        /// Determines if a vector image should be rasterised for display
        /// </summary>
        public bool Rasterize { get; set; } = true;

        public void EmbedFile()
        {
            // do nothing if there is no valid file specified
            if (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath)) return;

            if (!Rasterize)
            {
                using (var fileStream = new FileStream(FilePath, FileMode.Open, FileAccess.Read))
                {
                    _encodedData = new byte[fileStream.Length];
                    fileStream.Read(_encodedData, 0, (int)fileStream.Length);
                    fileStream.Position = 0;

                    var sourceSVG = new XPathDocument(fileStream);
                    var nav = sourceSVG.CreateNavigator();
                    if (nav.MoveToChild("svg", ""))
                    {
                        decimal.TryParse(nav.GetAttribute("width", ""), out decimal width);
                        decimal.TryParse(nav.GetAttribute("height", ""), out decimal height);

                        var paths = nav.SelectChildren("path", "");
                        var result = new TokenisedSVG();
                        result.Paths.Add(new PDFPath("[] 0 d\n"));

                        while (paths.MoveNext())
                        {
                            var path = paths.Current.GetAttribute("d", "");
                            var fill = paths.Current.GetAttribute("fill", "");
                            var strokeColor = paths.Current.GetAttribute("stroke", "");
                            if (!string.IsNullOrEmpty(strokeColor))
                            {
                                result.Paths.Add(new PDFPath(string.Format("{0} RG\n", ToPDFColor(strokeColor))));
                            }
                            else
                            {
                                result.Paths.Add(new PDFPath("0 0 0 RG\n"));
                            }

                            var strokeWidth = 1m;
                            if (!string.IsNullOrEmpty(paths.Current.GetAttribute("stroke-width", "")))
                            {
                                decimal.TryParse(paths.Current.GetAttribute("stroke-width", ""), out strokeWidth);
                            }
                            result.Paths.Add(new PDFPath(string.Format("{0} w\n", strokeWidth)));

                            // interpret the path
                            path = path.Replace(",", " ");
                            path = path.Replace("M", "M "); // Move To
                            path = path.Replace("m", "m ");
                            path = path.Replace("Z", "Z "); // Closepath
                            path = path.Replace("z", "z ");
                            path = path.Replace("L", "L "); // Line
                            path = path.Replace("l", "l ");
                            path = path.Replace("H", "H "); // Horizontal
                            path = path.Replace("h", "h ");
                            path = path.Replace("V", "V "); // Vertical
                            path = path.Replace("v", "v ");
                            path = path.Replace("C", "C "); // Cubic Bezier curve
                            path = path.Replace("c", "c ");
                            path = path.Replace("S", "S "); // Smoothed CBC TODO
                            path = path.Replace("s", "s ");
                            path = path.Replace("Q", "Q "); // Quadratic Bezier curve
                            path = path.Replace("q", "q ");
                            path = path.Replace("T", "T "); // Smoothed QBC TODO
                            path = path.Replace("t", "t ");
                            path = path.Replace("A", "A "); // Eliptical Arc TODO
                            path = path.Replace("a", "a ");

                            var pathElements = path.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                            var position = 0;
                            var posX = 0m;
                            var posY = 0m;
                            var startPosX = 0m;
                            var startPosY = 0m;
                            var inLineMode = false;

                            // TODO: polygons need to become paths and handle relative movements
                            while (position < pathElements.Length)
                            {
                                var element = pathElements[position];
                                switch (element)
                                {
                                    case "M":
                                    case "m":
                                        // execute the move-to command
                                        position++;
                                        posX = decimal.Parse(pathElements[position]);
                                        position++;
                                        posY = height - decimal.Parse(pathElements[position]);

                                        result.Paths.Add(new PDFPath("{0} {1} m\n", new List<PDFPathParam>()
                                        {
                                            new PDFPathParam
                                            {
                                                Value = posX,
                                                Operation = "+offsetX; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = posY,
                                                Operation = "+offsetY; *scale"
                                            }
                                        }));

                                        startPosX = posX;
                                        startPosY = posY;
                                        inLineMode = false;

                                        break;
                                    case "Z":
                                    case "z":
                                        // execute the close-path command
                                        result.Paths.Add(new PDFPath("{0} {1} l\n", new List<PDFPathParam>()
                                        {
                                            new PDFPathParam
                                            {
                                                Value = startPosX,
                                                Operation = "+offsetX; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = startPosY,
                                                Operation = "+offsetY; *scale"
                                            }
                                        }));
                                        inLineMode = false;

                                        break;
                                    case "L":
                                    case "l":
                                        // execute the line-to command
                                        position++;
                                        posX = decimal.Parse(pathElements[position]);
                                        position++;
                                        posY = height - decimal.Parse(pathElements[position]);

                                        result.Paths.Add(new PDFPath("{0} {1} l\n", new List<PDFPathParam>()
                                        {
                                            new PDFPathParam
                                            {
                                                Value = posX,
                                                Operation = "+offsetX; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = posY,
                                                Operation = "+offsetY; *scale"
                                            }
                                        }));

                                        //result.Paths.Add(new PDFPath("{0} {1} l\n", posX, posY));
                                        inLineMode = true;

                                        break;
                                    case "H":
                                    case "h":
                                        // execute the line-to command
                                        position++;
                                        posX = decimal.Parse(pathElements[position]);

                                        result.Paths.Add(new PDFPath("{0} {1} l\n", new List<PDFPathParam>()
                                        {
                                            new PDFPathParam
                                            {
                                                Value = posX,
                                                Operation = "+offsetX; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = posY,
                                                Operation = "+offsetY; *scale"
                                            }
                                        }));

                                        //result.Paths.Add(new PDFPath("{0} {1} l\n", posX, posY));
                                        inLineMode = true;

                                        break;
                                    case "V":
                                    case "v":
                                        // execute the line-to command
                                        position++;
                                        posY = height - decimal.Parse(pathElements[position]);

                                        result.Paths.Add(new PDFPath("{0} {1} l\n", new List<PDFPathParam>()
                                        {
                                            new PDFPathParam
                                            {
                                                Value = posX,
                                                Operation = "+offsetX; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = posY,
                                                Operation = "+offsetY; *scale"
                                            }
                                        }));

                                        //result.Paths.Add(new PDFPath("{0} {1} l\n", posX, posY));
                                        inLineMode = true;
                                        break;

                                    case "C":
                                    case "c":
                                        // execute the cubic bezier command
                                        position++;
                                        var cx1 = decimal.Parse(pathElements[position]);
                                        position++;
                                        var cy1 = height - decimal.Parse(pathElements[position]);

                                        position++;
                                        var cx2 = decimal.Parse(pathElements[position]);
                                        position++;
                                        var cy2 = height - decimal.Parse(pathElements[position]);

                                        position++;
                                        posX = decimal.Parse(pathElements[position]);
                                        position++;
                                        posY = height - decimal.Parse(pathElements[position]);

                                        result.Paths.Add(new PDFPath("{0} {1} {2} {3} {4} {5} c\n", new List<PDFPathParam>()
                                        {
                                            new PDFPathParam
                                            {
                                                Value = cx1,
                                                Operation = "+offsetX; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = cy1,
                                                Operation = "+offsetY; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = cx2,
                                                Operation = "+offsetY; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = cy2,
                                                Operation = "+offsetY; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = posX,
                                                Operation = "+offsetY; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = posY,
                                                Operation = "+offsetY; *scale"
                                            }
                                        }));

                                        //result.Paths.Add(new PDFPath("{0} {1} {2} {3} {4} {5} c\n", cx1, cy1, cx2, cy2, posX, posY));
                                        break;

                                    case "Q":
                                    case "q":
                                        // execute the quadratic bezier command
                                        position++;
                                        var qx1 = decimal.Parse(pathElements[position]);
                                        position++;
                                        var qy1 = height - decimal.Parse(pathElements[position]);

                                        // need to calculate the first cubic control points from start position 
                                        // and the quadratic control point
                                        var x1 = posX + (2 / 3 * (qx1 - posX));
                                        var y1 = posY + (2 / 3 * (qy1 - posY));

                                        position++;
                                        posX = decimal.Parse(pathElements[position]);
                                        position++;
                                        posY = height - decimal.Parse(pathElements[position]);

                                        // need to calculate the second cubic control points from end position 
                                        // and the quadratic control point
                                        var x2 = posX + (2 / 3 * (qx1 - posX));
                                        var y2 = posY + (2 / 3 * (qy1 - posY));

                                        result.Paths.Add(new PDFPath("{0} {1} {2} {3} {4} {5} c\n", new List<PDFPathParam>()
                                        {
                                            new PDFPathParam
                                            {
                                                Value = x1,
                                                Operation = "+offsetX; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = y1,
                                                Operation = "+offsetY; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = x2,
                                                Operation = "+offsetY; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = y2,
                                                Operation = "+offsetY; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = posX,
                                                Operation = "+offsetY; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = posY,
                                                Operation = "+offsetY; *scale"
                                            }
                                        }));

                                        //result.Paths.Add(new PDFPath("{0} {1} {2} {3} {4} {5} c\n", x1, y1, x2, y2, posX, posY));
                                        break;

                                    default:
                                        // not a command so it must be a set of coordinates
                                        if (inLineMode)
                                        {
                                            posX = decimal.Parse(pathElements[position]);
                                            position++;
                                            posY = height - decimal.Parse(pathElements[position]);

                                            result.Paths.Add(new PDFPath("{0} {1} l\n", new List<PDFPathParam>()
                                            {
                                                new PDFPathParam
                                                {
                                                    Value = posX,
                                                    Operation = "+offsetX; *scale"
                                                },
                                                new PDFPathParam
                                                {
                                                    Value = posY,
                                                    Operation = "+offsetY; *scale"
                                                }
                                            }));

                                            //result.Paths.Add(new PDFPath("{0} {1} l\n", posX, posY));
                                        }
                                        break;
                                }

                                position++;
                            }
                        }

                        result.Paths.Add(new PDFPath("b"));

                        ByteData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result));
                    }

                }

                return;
            }

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
            if (Rasterize)
            {
                // only need to do this if the image has been rasterised
                // if it hasn't then the file has already been encoded 
                // into the PDF object
                _encodedData = ByteData;
            }

            base.PrepareStream(compress);

            if (MaskData != null)
            {
                MaskData.PrepareStream(compress);
            }
        }

        public override void Publish(StreamWriter stream)
        {
            if (Rasterize)
            {
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
            }
            else
            {
                // save the svg file here
                var PDFData = new Dictionary<string, dynamic>
                {
                    { "/Type", "/EmbeddedFile" },
                    { "/Subtype", "/image#2Fsvg+xml"},
                };

                _pdfObject = PDFData;
            }

            base.Publish(stream);

            if (MaskData != null)
            {
                // if there is an associated mask then publish that as well
                MaskData.Publish(stream);
            }
        }
    }

}
