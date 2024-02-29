using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;

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
            // do nothing if there is no valid file or file data specified
            if (FileData == null && (string.IsNullOrEmpty(FilePath) || !File.Exists(FilePath))) return;

            var rasterize = true;

            // attempt to load the image file into an xml document. If it works then this is 
            // an svg file and needs to be parsed differently
            using (var fileStream = Open())
            {
                try
                {
                    var sourceSVG = new XmlDocument();
                    sourceSVG.Load(fileStream);
                    rasterize = false;
                }
                catch
                {
                    // any exception here means that this is not a valid SVG file
                    rasterize = true;
                }
            }

            if (!rasterize)
            {
                using (var fileStream = Open())
                {
                    _encodedData = new byte[fileStream.Length];
                    fileStream.Read(_encodedData, 0, (int)fileStream.Length);
                    fileStream.Position = 0;

                    Type = PATHDATA;

                    var sourceSVG = new XmlDocument();
                    sourceSVG.Load(fileStream);

                    var nav = sourceSVG.CreateNavigator();
                    var svgns = "";
                    var foundRoot = false;

                    if (nav.MoveToChild("svg", "http://www.w3.org/2000/svg"))
                    {
                        foundRoot = true;
                        svgns = "http://www.w3.org/2000/svg";
                    }
                    else
                    {
                        foundRoot = nav.MoveToChild("svg", svgns);
                    }

                    if (foundRoot)
                    {
                        decimal.TryParse(nav.GetAttribute("width", ""), out decimal width);
                        decimal.TryParse(nav.GetAttribute("height", ""), out decimal height);

                        if (height == 0 || width == 0)
                        {
                            // no height or width specified check for a viewbox
                            var viewBox = nav.GetAttribute("viewBox", "");
                            if (!string.IsNullOrEmpty(viewBox))
                            {
                                var dimensions = viewBox.Split(' ');

                                if (dimensions.Length == 4)
                                {
                                    width = decimal.Parse(dimensions[2]);
                                    height = decimal.Parse(dimensions[3]);
                                }
                            }
                            else
                            {
                                throw new Exception("SVG does not specify any dimensions");
                            }
                        }

                        Height = (int)height;
                        Width = (int)width;
                        var Background = "";

                        var overallStyle = nav.GetAttribute("style", "");
                        if (!string.IsNullOrEmpty(overallStyle))
                        {
                            var styleElements = overallStyle.Split(';');
                            foreach (var element in styleElements)
                            {
                                // split these further into attribute and value properties
                                var parts = element.Split(':');
                                if (parts[0].ToLower() == "background")
                                {
                                    Background = parts[1].Trim();
                                }
                            }
                        }

                        // TODO: handle transform attributes

                        // go through the file and get rid of any group elements
                        var groups = nav.SelectChildren("g", svgns);
                        do
                        {
                            // TODO: groups might have attributes like stroke or fill that will need to be
                            // passed down into the children

                            while (groups.MoveNext())
                            {
                                var group = groups.Current.InnerXml;
                                groups.Current.ReplaceSelf(group);
                            }

                            groups = nav.SelectChildren("g", svgns);
                        } while (groups.Count > 0);

                        var circles = nav.SelectChildren("circle", svgns);
                        while (circles.MoveNext())
                        {
                            var cx = circles.Current.GetAttribute("cx", "");
                            var cy = circles.Current.GetAttribute("cy", "");
                            var radius = circles.Current.GetAttribute("r", "");
                            var startX = decimal.Parse(cx) - decimal.Parse(radius);
                            var diameter = decimal.Parse(radius) * 2;

                            var style = circles.Current.GetAttribute("style", "");
                            if (!string.IsNullOrEmpty(style))
                            {
                                style = string.Format("style='{0}'", style);
                            }

                            var strokewidth = circles.Current.GetAttribute("stroke-width", "");
                            if (!string.IsNullOrEmpty(strokewidth))
                            {
                                strokewidth = string.Format("stroke-width='{0}'", strokewidth);
                            }

                            var stroke = circles.Current.GetAttribute("stroke", "");
                            if (!string.IsNullOrEmpty(stroke))
                            {
                                stroke = string.Format("stroke='{0}'", stroke);
                            }

                            var fill = circles.Current.GetAttribute("fill", "");
                            if (string.IsNullOrEmpty(fill))
                            {
                                fill = "fill='none'";
                            }
                            else
                            {
                                fill = string.Format("fill='{0}'", fill);
                            }

                            var path = "M ";
                            path += string.Format("{0} {1} a {2} {2} 0 1 0 {3} 0 a {2} {2} 0 1 0 -{3} 0", startX, cy, radius, diameter);

                            circles.Current.ReplaceSelf(string.Format("<path d='{0}' {1} {2} {3} {4}/>", path, stroke, strokewidth, fill, style));
                        }

                        var ellipses = nav.SelectChildren("ellipse", svgns);
                        while (ellipses.MoveNext())
                        {
                            var cx = ellipses.Current.GetAttribute("cx", "");
                            var cy = ellipses.Current.GetAttribute("cy", "");
                            var rx = ellipses.Current.GetAttribute("rx", "");
                            var ry = ellipses.Current.GetAttribute("ry", "");
                            var startX = decimal.Parse(cx) - decimal.Parse(rx);
                            var dx = decimal.Parse(rx) * 2;

                            var style = circles.Current.GetAttribute("style", "");
                            if (!string.IsNullOrEmpty(style))
                            {
                                style = string.Format("style='{0}'", style);
                            }

                            var strokewidth = ellipses.Current.GetAttribute("stroke-width", "");
                            if (!string.IsNullOrEmpty(strokewidth))
                            {
                                strokewidth = string.Format("stroke-width='{0}'", strokewidth);
                            }

                            var stroke = ellipses.Current.GetAttribute("stroke", "");
                            if (!string.IsNullOrEmpty(stroke))
                            {
                                stroke = string.Format("stroke='{0}'", stroke);
                            }

                            var fill = ellipses.Current.GetAttribute("fill", "");
                            if (string.IsNullOrEmpty(fill))
                            {
                                fill = "fill='none'";
                            }
                            else
                            {
                                fill = string.Format("fill='{0}'", fill);
                            }

                            var path = "M ";
                            path += string.Format("{0} {1} a {2} {3} 0 1 0 {4} 0 a {2} {3} 0 1 0 -{4} 0", startX, cy, rx, ry, dx);

                            ellipses.Current.ReplaceSelf(string.Format("<path d='{0}' {1} {2} {3} {4}/>", path, stroke, strokewidth, fill, style));
                        }

                        // go through the lines and convert these to paths
                        var lines = nav.SelectChildren("line", svgns);
                        while (lines.MoveNext())
                        {
                            var startX = lines.Current.GetAttribute("x1", "");
                            var startY = lines.Current.GetAttribute("y1", "");
                            var endX = lines.Current.GetAttribute("x2", "");
                            var endY = lines.Current.GetAttribute("y2", "");

                            var style = circles.Current.GetAttribute("style", "");
                            if (!string.IsNullOrEmpty(style))
                            {
                                style = string.Format("style='{0}'", style);
                            }

                            var strokewidth = lines.Current.GetAttribute("stroke-width", "");
                            if (!string.IsNullOrEmpty(strokewidth))
                            {
                                strokewidth = string.Format("stroke-width='{0}'", strokewidth);
                            }

                            var stroke = lines.Current.GetAttribute("stroke", "");
                            if (!string.IsNullOrEmpty(stroke))
                            {
                                stroke = string.Format("stroke='{0}'", stroke);
                            }

                            var path = "M ";
                            path += string.Format("{0} {1} L {2} {3}", startX, startY, endX, endY);

                            lines.Current.ReplaceSelf(string.Format("<path d='{0}' {1} {2} {3}/>", path, stroke, strokewidth, style));
                        }

                        // go through the polygons and convert them to paths
                        var polygons = nav.SelectChildren("polygon", svgns);
                        while (polygons.MoveNext())
                        {
                            var style = circles.Current.GetAttribute("style", "");
                            if (!string.IsNullOrEmpty(style))
                            {
                                style = string.Format("style='{0}'", style);
                            }

                            var fill = polygons.Current.GetAttribute("fill", "");
                            if (string.IsNullOrEmpty(fill))
                            {
                                fill = "fill='none'";
                            }
                            else
                            {
                                fill = string.Format("fill='{0}'", fill);
                            }

                            var stroke = polygons.Current.GetAttribute("stroke", "");
                            if (!string.IsNullOrEmpty(stroke))
                            {
                                stroke = string.Format("stroke='{0}'", stroke);
                            }

                            var strokewidth = polygons.Current.GetAttribute("stroke-width", "");
                            if (!string.IsNullOrEmpty(strokewidth))
                            {
                                strokewidth = string.Format("stroke-width='{0}'", strokewidth);
                            }

                            var polygon = polygons.Current.GetAttribute("points","");
                            polygon = polygon.Replace(",", " ");

                            var points = polygon.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            var path = "M ";
                            for (var i = 0; i < points.Length; i++ )
                            {
                                if (i == 2)
                                {
                                    path += "L ";
                                }

                                path += points[i].ToString() + " ";
                            }
                            path += "Z";

                            polygons.Current.ReplaceSelf(string.Format("<path d='{0}' {1} {2} {3} {4}/>", path, fill, stroke, strokewidth, style));
                        }

                        // go through the polylines and convert them to paths. This is similar to
                        // the polygon case but the path is not closed (no Z at the end)
                        var polylines = nav.SelectChildren("polyline", svgns);
                        while (polylines.MoveNext())
                        {
                            var style = circles.Current.GetAttribute("style", "");
                            if (!string.IsNullOrEmpty(style))
                            {
                                style = string.Format("style='{0}'", style);
                            }

                            var fill = polygons.Current.GetAttribute("fill", "");
                            if (string.IsNullOrEmpty(fill))
                            {
                                fill = "fill='none'";
                            }
                            else
                            {
                                fill = string.Format("fill='{0}'", fill);
                            }

                            var stroke = polygons.Current.GetAttribute("stroke", "");
                            if (!string.IsNullOrEmpty(stroke))
                            {
                                stroke = string.Format("stroke='{0}'", stroke);
                            }

                            var strokewidth = polygons.Current.GetAttribute("stroke-width", "");
                            if (!string.IsNullOrEmpty(strokewidth))
                            {
                                strokewidth = string.Format("stroke-width='{0}'", strokewidth);
                            }

                            var polyline = polygons.Current.GetAttribute("points", "");
                            polyline = polyline.Replace(",", " ");

                            var points = polyline.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            var path = "M ";
                            for (var i = 0; i < points.Length; i++)
                            {
                                if (i == 2)
                                {
                                    path += "L ";
                                }

                                path += points[i].ToString() + " ";
                            }

                            polylines.Current.ReplaceSelf(string.Format("<path d='{0}' {1} {2} {3} {4}/>", path, fill, stroke, strokewidth, style));
                        }

                        // go through the rectangles and convert them to paths also
                        var rects = nav.SelectChildren("rect", svgns);
                        while (rects.MoveNext())
                        {
                            var startX = rects.Current.GetAttribute("x", "");
                            var startY = rects.Current.GetAttribute("y", "");
                            var rectWidth = rects.Current.GetAttribute("width", "");
                            var rectHeight = rects.Current.GetAttribute("width", "");

                            var style = circles.Current.GetAttribute("style", "");
                            if (!string.IsNullOrEmpty(style))
                            {
                                style = string.Format("style='{0}'", style);
                            }

                            var fill = rects.Current.GetAttribute("fill", "");
                            if (string.IsNullOrEmpty(fill))
                            {
                                fill = "fill='none'";
                            }
                            else
                            {
                                fill = string.Format("fill='{0}'", fill);
                            }
                            var stroke = rects.Current.GetAttribute("stroke", "");
                            if (!string.IsNullOrEmpty(stroke))
                            {
                                stroke = string.Format("stroke='{0}'", stroke);
                            }
                            var strokewidth = rects.Current.GetAttribute("stroke-width", "");
                            if (!string.IsNullOrEmpty(strokewidth))
                            {
                                strokewidth = string.Format("stroke-width='{0}'", strokewidth);
                            }

                            var path = "M ";
                            path += string.Format("{0} {1} h {2} v {3} h -{4} ", startX, startY, width, height, width);
                            path += "Z ";
                            rects.Current.ReplaceSelf(string.Format("<path d='{0}' {1} {2} {3} {4}/>", path, fill, stroke, strokewidth, style));
                        }

                        var paths = nav.SelectChildren("path", svgns);
                        var result = new TokenisedSVG();
                        result.Paths.Add(new PDFPath("[] 0 d\n"));

                        if (!string.IsNullOrEmpty(Background))
                        {
                            // there is a background color specified in the SVG
                            result.Paths.Add(new PDFPath(string.Format("{0} rg\n", ToPDFColor(Background))));
                            result.Paths.Add(new PDFPath("{0} {1} m\n", new List<PDFPathParam>()
                                        {
                                            new PDFPathParam
                                            {
                                                Value = 0,
                                                Operation = "+offsetX; *scale"
                                            },
                                            new PDFPathParam
                                            {
                                                Value = 0,
                                                Operation = "+offsetY; *scale"
                                            }
                                        }));
                            result.Paths.Add(new PDFPath("{0} {1} l\n", new List<PDFPathParam>()
                                            {
                                                new PDFPathParam
                                                {
                                                    Value = 0,
                                                    Operation = "+offsetX; *scale"
                                                },
                                                new PDFPathParam
                                                {
                                                    Value = Height,
                                                    Operation = "+offsetY; *scale"
                                                }
                                            }));
                            result.Paths.Add(new PDFPath("{0} {1} l\n", new List<PDFPathParam>()
                                            {
                                                new PDFPathParam
                                                {
                                                    Value = Width,
                                                    Operation = "+offsetX; *scale"
                                                },
                                                new PDFPathParam
                                                {
                                                    Value = Height,
                                                    Operation = "+offsetY; *scale"
                                                }
                                            }));
                            result.Paths.Add(new PDFPath("{0} {1} l\n", new List<PDFPathParam>()
                                            {
                                                new PDFPathParam
                                                {
                                                    Value = Width,
                                                    Operation = "+offsetX; *scale"
                                                },
                                                new PDFPathParam
                                                {
                                                    Value = 0,
                                                    Operation = "+offsetY; *scale"
                                                }
                                            }));
                            result.Paths.Add(new PDFPath("f\n"));
                        }

                        var posX = 0m;
                        var posY = 0m;
                        var startPosX = 0m;
                        var startPosY = 0m;

                        while (paths.MoveNext())
                        {
                            var path = paths.Current.GetAttribute("d", "");
                            var fill = "none";
                            var strokeColor = "";
                            var linecap = "";

                            var style = paths.Current.GetAttribute("style", "");
                            if (!string.IsNullOrEmpty(style))
                            {
                                var styleElements = style.Split(';');
                                foreach (var element in styleElements)
                                {
                                    // split these further into attribute and value properties
                                    var parts = element.Split(':');

                                    // can't add an attribute that already exists
                                    var value = paths.Current.GetAttribute(parts[0], "");
                                    if (string.IsNullOrEmpty(value))
                                    {
                                        paths.Current.CreateAttribute("", parts[0], "", parts[1]);
                                    }
                                    else
                                    {
                                        var xml = paths.Current.OuterXml.Replace(parts[0] + "=\"" + value, parts[0] + "=\"" + parts[1]);
                                        paths.Current.ReplaceSelf(xml);
                                    }
                                }
                            }

                            fill = paths.Current.GetAttribute("fill", "");
                            if (string.IsNullOrEmpty(fill))
                            {
                                fill = "none";
                            }

                            if (fill.ToLower() != "none")
                            {
                                result.Paths.Add(new PDFPath(string.Format("{0} rg\n", ToPDFColor(fill))));
                            }

                            // todo: this is technically not correct... if there is no stroke there really should be no stroke
                            // but this is here for now until the CSS/style type attributes are handled.
                            strokeColor = paths.Current.GetAttribute("stroke", "");
                            if (string.IsNullOrEmpty(strokeColor))
                            {
                                // set it to match the fill color if there is no specific color
                                strokeColor = paths.Current.GetAttribute("fill", "");
                                if (string.IsNullOrEmpty(strokeColor))
                                {
                                    // if it's still not specified set it to black
                                    strokeColor = "black";
                                }
                            }
                            result.Paths.Add(new PDFPath(string.Format("{0} RG\n", ToPDFColor(strokeColor))));

                            var strokeWidth = 1m;
                            if (!string.IsNullOrEmpty(paths.Current.GetAttribute("stroke-width", "")))
                            {
                                decimal.TryParse(paths.Current.GetAttribute("stroke-width", ""), out strokeWidth);
                            }

                            result.Paths.Add(new PDFPath("{0} w\n", new List<PDFPathParam>
                                    {
                                        new PDFPathParam
                                        {
                                            Value = strokeWidth,
                                            Operation = "*scale;"
                                        }
                                    }
                            ));

                            linecap = paths.Current.GetAttribute("stroke-linecap", "");
                            if (!string.IsNullOrEmpty(linecap))
                            {
                                if (linecap.ToLower() == "butt")
                                {
                                    result.Paths.Add(new PDFPath("0 J\n"));
                                }
                                if (linecap.ToLower() == "round")
                                {
                                    result.Paths.Add(new PDFPath("1 J\n"));
                                }
                                if (linecap.ToLower() == "square")
                                {
                                    result.Paths.Add(new PDFPath("2 J\n"));
                                }
                            }

                            // interpret the path
                            path = path.Replace(",", " ");
                            path = path.Replace("-", " -");
                            path = path.Replace("-.", "-0.");
                            path = path.Replace("\n", " ");
                            path = path.Replace("\r", " ");
                            path = path.Replace("M", " M "); // Move To
                            path = path.Replace("m", " m ");
                            path = path.Replace("Z", " Z "); // Closepath
                            path = path.Replace("z", " z ");
                            path = path.Replace("L", " L "); // Line
                            path = path.Replace("l", " l ");
                            path = path.Replace("H", " H "); // Horizontal
                            path = path.Replace("h", " h ");
                            path = path.Replace("V", " V "); // Vertical
                            path = path.Replace("v", " v ");
                            path = path.Replace("C", " C "); // Cubic Bezier curve
                            path = path.Replace("c", " c ");
                            path = path.Replace("S", " S "); // Smoothed CBC
                            path = path.Replace("s", " s ");
                            path = path.Replace("Q", " Q "); // Quadratic Bezier curve
                            path = path.Replace("q", " q ");
                            path = path.Replace("T", " T "); // Smoothed QBC
                            path = path.Replace("t", " t ");
                            path = path.Replace("A", " A "); // Eliptical Arc
                            path = path.Replace("a", " a ");

                            var rawElements = path.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);
                            var position = 0;

                            // need to loop through the path elements and clean up any that might still look weird
                            // svg allows path entries like M.50.11h10.50.78 which will result in some elements appearing 
                            // to have two decimal places... these should be split into two numbers 
                            // (eg: 10.50.78 becomes 10.50 & 0.78)
                            var pathElements = new List<string>();
                            for (var i = 0; i < rawElements.Length; i++)
                            {
                                var element = rawElements[i];
                                if (element.Count(e => e == '.') > 1)
                                {
                                    var first = element.Substring(0, element.LastIndexOf('.'));
                                    pathElements.Add(first);

                                    element = "0" + element.Substring(element.LastIndexOf('.'));
                                }

                                pathElements.Add(element);
                            }

                            decimal? cx1 = null;
                            decimal? cx2 = null;
                            decimal? cy1 = null;
                            decimal? cy2 = null;
                            decimal? qx1 = null;
                            decimal? qy1 = null;

                            while (position < pathElements.Count)
                            {

                                var element = pathElements[position];
                                switch (element)
                                {
                                    case "A":
                                    case "a":
                                        // because it is possible for there to be one arc path after another this controll needs to 
                                        // loop while there are still numbers after what is supposedly the last number
                                        do
                                        {
                                            position++;
                                            var rx = double.Parse(pathElements[position]); // x-radius
                                            position++;
                                            var ry = double.Parse(pathElements[position]); // y-radius
                                            position++;
                                            var xRot = double.Parse(pathElements[position]); // x-rotation
                                            position++;
                                            var largeArc = int.Parse(pathElements[position]); // large-arc flag
                                            position++;
                                            var sweep = int.Parse(pathElements[position]); // sweep flag 

                                            var startX = (double)posX;
                                            var startY = (double)(height - posY); // do not reverse the y co-ordinates until after the math has been done

                                            // execute the elliptical arc command
                                            if (element == "a")
                                            {
                                                // relative
                                                position++;
                                                posX = posX + decimal.Parse(pathElements[position]);
                                                position++;
                                                posY = posY + (0 - decimal.Parse(pathElements[position]));
                                            }
                                            else
                                            {
                                                // absolute
                                                position++;
                                                posX = decimal.Parse(pathElements[position]);
                                                position++;
                                                posY = height - decimal.Parse(pathElements[position]);
                                            }

                                            var endX = (double)posX;
                                            var endY = (double)(height - posY); // do not reverse the y co-ordinates until after the math has been done

                                            var curves = ArcToBezier(new Point(startX, startY), new Point(endX, endY), rx, ry, xRot, largeArc, sweep);

                                            foreach (var curve in curves)
                                            {
                                                result.Paths.Add(new PDFPath("{0} {1} {2} {3} {4} {5} c\n", new List<PDFPathParam>()
                                                {
                                                    new PDFPathParam
                                                    {
                                                        Value = (decimal)curve.Cp1.X,
                                                        Operation = "+offsetX; *scale;"
                                                    },
                                                    new PDFPathParam
                                                    {
                                                        Value = height - (decimal)curve.Cp1.Y,
                                                        Operation = "+offsetY; *scale"
                                                    },
                                                    new PDFPathParam
                                                    {
                                                        Value = (decimal)curve.Cp2.X,
                                                        Operation = "+offsetX; *scale"
                                                    },
                                                    new PDFPathParam
                                                    {
                                                        Value = height - (decimal)curve.Cp2.Y,
                                                        Operation = "+offsetY; *scale"
                                                    },
                                                    new PDFPathParam
                                                    {
                                                        Value = (decimal)curve.End.X,
                                                        Operation = "+offsetX; *scale"
                                                    },
                                                    new PDFPathParam
                                                    {
                                                        Value = height - (decimal)curve.End.Y,
                                                        Operation = "+offsetY; *scale"
                                                    }
                                                }));
                                            }
                                        } while ((position + 1 < pathElements.Count) && decimal.TryParse(pathElements[position + 1], out decimal nextArc));

                                        break;

                                    case "M":
                                    case "m":
                                        // execute the move-to command
                                        if (element == "m")
                                        {
                                            // relative
                                            position++;
                                            posX = posX + decimal.Parse(pathElements[position]);
                                            position++;
                                            posY = posY + (0 - decimal.Parse(pathElements[position]));
                                        }
                                        else
                                        {
                                            // absolute
                                            position++;
                                            posX = decimal.Parse(pathElements[position]);
                                            position++;
                                            posY = height - decimal.Parse(pathElements[position]);
                                        }

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

                                        // reset the control points
                                        cx1 = null;
                                        cy1 = null;
                                        cx2 = null;
                                        cy2 = null;
                                        qx1 = null;
                                        qy1 = null;

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

                                        posX = startPosX;
                                        posY = startPosY;

                                        break;
                                    case "L":
                                    case "l":
                                        // execute the line-to command
                                        do
                                        { 
                                            if (element == "l")
                                            {
                                                // relative
                                                position++;
                                                posX = posX + decimal.Parse(pathElements[position]);
                                                position++;
                                                posY = posY + (0 - decimal.Parse(pathElements[position]));
                                            }
                                            else
                                            {
                                                // absolute
                                                position++;
                                                posX = decimal.Parse(pathElements[position]);
                                                position++;
                                                posY = height - decimal.Parse(pathElements[position]);
                                            }

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
                                        } while ((position + 1 < pathElements.Count) && decimal.TryParse(pathElements[position + 1], out decimal nextLine)) ;

                                        break;
                                    case "H":
                                    case "h":
                                        // execute the line-to command
                                        do
                                        { 
                                            if (element == "h")
                                            {
                                                // relative
                                                position++;
                                                posX = posX + decimal.Parse(pathElements[position]);
                                            }
                                            else
                                            {
                                                // absolute
                                                position++;
                                                posX = decimal.Parse(pathElements[position]);
                                            }

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
                                        } while ((position + 1 < pathElements.Count) && decimal.TryParse(pathElements[position + 1], out decimal nextLine)) ;

                                        break;
                                    case "V":
                                    case "v":
                                        // execute the line-to command
                                        do
                                        {
                                            if (element == "v")
                                            {
                                                // relative
                                                position++;
                                                posY = posY + (0 - decimal.Parse(pathElements[position]));
                                            }
                                            else
                                            {
                                                // absolute
                                                position++;
                                                posY = height - decimal.Parse(pathElements[position]);
                                            }

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

                                        } while ((position + 1 < pathElements.Count) && decimal.TryParse(pathElements[position + 1], out decimal nextLine));

                                        break;

                                    case "C":
                                    case "c":
                                        // execute the cubic bezier command
                                        do
                                        {
                                            if (element == "c")
                                            {
                                                // relative
                                                position++;
                                                cx1 = posX + decimal.Parse(pathElements[position]);
                                                position++;
                                                cy1 = posY + (0 - decimal.Parse(pathElements[position]));

                                                position++;
                                                cx2 = posX + decimal.Parse(pathElements[position]);
                                                position++;
                                                cy2 = posY + (0 - decimal.Parse(pathElements[position]));

                                                position++;
                                                posX = posX + decimal.Parse(pathElements[position]);
                                                position++;
                                                posY = posY + (0 - decimal.Parse(pathElements[position]));
                                            }
                                            else
                                            {
                                                // absolute
                                                position++;
                                                cx1 = decimal.Parse(pathElements[position]);
                                                position++;
                                                cy1 = height - decimal.Parse(pathElements[position]);

                                                position++;
                                                cx2 = decimal.Parse(pathElements[position]);
                                                position++;
                                                cy2 = height - decimal.Parse(pathElements[position]);

                                                position++;
                                                posX = decimal.Parse(pathElements[position]);
                                                position++;
                                                posY = height - decimal.Parse(pathElements[position]);
                                            }

                                            result.Paths.Add(new PDFPath("{0} {1} {2} {3} {4} {5} c\n", new List<PDFPathParam>()
                                            {
                                                new PDFPathParam
                                                {
                                                    Value = cx1.Value,
                                                    Operation = "+offsetX; *scale"
                                                },
                                                new PDFPathParam
                                                {
                                                    Value = cy1.Value,
                                                    Operation = "+offsetY; *scale"
                                                },
                                                new PDFPathParam
                                                {
                                                    Value = cx2.Value,
                                                    Operation = "+offsetX; *scale"
                                                },
                                                new PDFPathParam
                                                {
                                                    Value = cy2.Value,
                                                    Operation = "+offsetY; *scale"
                                                },
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
                                        } while ((position + 1 < pathElements.Count) && decimal.TryParse(pathElements[position + 1], out decimal nextCurve));

                                        break;

                                    case "S":
                                    case "s":
                                        // execute the smoothed cubic bezier command
                                        do
                                        {
                                            // calculate the first control point. This is assumed to be the reflection of the second control point
                                            // of the previous command (assuming the previous command was a C/c or S/s. 
                                            if (cx2 != null && cy2 != null)
                                            {
                                                var diffX = posX - cx2;
                                                var diffY = posY - cy2;

                                                cx1 = posX + diffX;
                                                cy1 = posY + diffY;
                                            }

                                            if (element == "s")
                                            {
                                                // relative
                                                position++;
                                                cx2 = posX + decimal.Parse(pathElements[position]);
                                                position++;
                                                cy2 = posY + (0 - decimal.Parse(pathElements[position]));

                                                position++;
                                                posX = posX + decimal.Parse(pathElements[position]);
                                                position++;
                                                posY = posY + (0 - decimal.Parse(pathElements[position]));
                                            }
                                            else
                                            {
                                                // absolute
                                                position++;
                                                cx2 = decimal.Parse(pathElements[position]);
                                                position++;
                                                cy2 = height - decimal.Parse(pathElements[position]);

                                                position++;
                                                posX = decimal.Parse(pathElements[position]);
                                                position++;
                                                posY = height - decimal.Parse(pathElements[position]);
                                            }

                                            // If there was no previous curve command then the control point is coincident with 
                                            // the current control point
                                            if (cx1 == null && cy1 == null)
                                            {
                                                cx1 = cx2;
                                                cy1 = cy2;
                                            }

                                            result.Paths.Add(new PDFPath("{0} {1} {2} {3} {4} {5} c\n", new List<PDFPathParam>()
                                            {
                                                new PDFPathParam
                                                {
                                                    Value = cx1.Value,
                                                    Operation = "+offsetX; *scale"
                                                },
                                                new PDFPathParam
                                                {
                                                    Value = cy1.Value,
                                                    Operation = "+offsetY; *scale"
                                                },
                                                new PDFPathParam
                                                {
                                                    Value = cx2.Value,
                                                    Operation = "+offsetX; *scale"
                                                },
                                                new PDFPathParam
                                                {
                                                    Value = cy2.Value,
                                                    Operation = "+offsetY; *scale"
                                                },
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
                                        } while ((position + 1 < pathElements.Count) && decimal.TryParse(pathElements[position + 1], out decimal nextCurve));

                                        break;

                                    case "Q":
                                    case "q":
                                        // execute the quadratic bezier command
                                        do
                                        {
                                            var x1 = 0m;
                                            var x2 = 0m;
                                            var y1 = 0m;
                                            var y2 = 0m;

                                            if (element == "q")
                                            {
                                                // relative
                                                position++;
                                                qx1 = posX + decimal.Parse(pathElements[position]);
                                                position++;
                                                qy1 = posY + (0 - decimal.Parse(pathElements[position]));

                                                // need to calculate the first cubic control points from start position 
                                                // and the quadratic control point
                                                x1 = posX + (2m / 3m * (qx1.Value - posX));
                                                y1 = posY + (2m / 3m * (qy1.Value - posY));

                                                position++;
                                                posX = posX + decimal.Parse(pathElements[position]);
                                                position++;
                                                posY = posY + (0 - decimal.Parse(pathElements[position]));
                                            }
                                            else
                                            {
                                                // absolute
                                                position++;
                                                qx1 = decimal.Parse(pathElements[position]);
                                                position++;
                                                qy1 = height - decimal.Parse(pathElements[position]);

                                                // need to calculate the first cubic control points from start position 
                                                // and the quadratic control point
                                                x1 = posX + (2m / 3m * (qx1.Value - posX));
                                                y1 = posY + (2m / 3m * (qy1.Value - posY));

                                                position++;
                                                posX = decimal.Parse(pathElements[position]);
                                                position++;
                                                posY = height - decimal.Parse(pathElements[position]);
                                            }

                                            // need to calculate the second cubic control points from end position 
                                            // and the quadratic control point
                                            x2 = posX + (2m / 3m * (qx1.Value - posX));
                                            y2 = posY + (2m / 3m * (qy1.Value - posY));

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
                                                    Operation = "+offsetX; *scale"
                                                },
                                                new PDFPathParam
                                                {
                                                    Value = y2,
                                                    Operation = "+offsetY; *scale"
                                                },
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

                                        } while ((position + 1 < pathElements.Count) && decimal.TryParse(pathElements[position + 1], out decimal nextCurve));


                                        break;
                                    case "T":
                                    case "t":
                                        // execute the smoothed quadratic bezier command
                                        do
                                        {
                                            var x1 = 0m;
                                            var x2 = 0m;
                                            var y1 = 0m;
                                            var y2 = 0m;

                                            // calculate the first control point. This is assumed to be the reflection of the second control point
                                            // of the previous command (assuming the previous command was a Q/q or T/t. 
                                            if (qx1 != null && qy1 != null)
                                            {
                                                var diffX = posX - qx1;
                                                var diffY = posY - qy1;

                                                qx1 = posX + diffX;
                                                qy1 = posY + diffY;
                                            }

                                            // need to calculate the first cubic control points from start position 
                                            // and the quadratic control point
                                            x1 = posX + (2m / 3m * (qx1.Value - posX));
                                            y1 = posY + (2m / 3m * (qy1.Value - posY));

                                            if (element == "t")
                                            {
                                                // relative

                                                position++;
                                                posX = posX + decimal.Parse(pathElements[position]);
                                                position++;
                                                posY = posY + (0 - decimal.Parse(pathElements[position]));
                                            }
                                            else
                                            {
                                                // absolute

                                                position++;
                                                posX = decimal.Parse(pathElements[position]);
                                                position++;
                                                posY = height - decimal.Parse(pathElements[position]);
                                            }

                                            // need to calculate the second cubic control points from end position 
                                            // and the quadratic control point
                                            x2 = posX + (2m / 3m * (qx1.Value - posX));
                                            y2 = posY + (2m / 3m * (qy1.Value - posY));

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
                                                    Operation = "+offsetX; *scale"
                                                },
                                                new PDFPathParam
                                                {
                                                    Value = y2,
                                                    Operation = "+offsetY; *scale"
                                                },
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

                                        } while ((position + 1 < pathElements.Count) && decimal.TryParse(pathElements[position + 1], out decimal nextCurve));

                                        break;
                                    default:
                                        break;
                                }

                                position++;
                            }

                            if (fill != "none" && !string.IsNullOrEmpty(strokeColor))
                            {
                                // close, fill and stroke the path
                                result.Paths.Add(new PDFPath("b\n"));
                            }
                            else if (!string.IsNullOrEmpty(strokeColor))
                            {
                                // just stroke the path
                                result.Paths.Add(new PDFPath("S\n"));
                            }
                            else if (fill != "none")
                            {
                                // just fill the path
                                result.Paths.Add(new PDFPath("f\n"));
                            }
                        }

                        ByteData = Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(result));
                    }

                }

                return;
            }

            // load the image from the file or from the passed in data
            using (var image = (FileData == null ? Image.Load<Rgba32>(FilePath) : Image.Load<Rgba32>(FileData)))
            {
                Height = image.Height;
                Width = image.Width;

                var hasAlpha = false;


                byte[] rgbbuf = new byte[image.Width * image.Height * Unsafe.SizeOf<Rgba32>()];
                image.CopyPixelDataTo(rgbbuf);

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
            if (Type != PATHDATA)
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
            if (Type != PATHDATA)
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

        const double TAU = Math.PI * 2f;

        /// <summary>
        /// Maps the unit circle co-ordinates back to their original position, rotation and scales
        /// </summary>
        private Point MapBackToOriginal(Point p, double rx, double ry, double cosphi, double sinphi, Point centrePos)
        {
            // Scale back out by the radii
            p.X *= rx;
            p.Y *= ry;

            // rotate back to orginial angles
            var xp = (cosphi * p.X) - (sinphi * p.Y);
            var yp = (sinphi * p.X) + (cosphi * p.Y);

            // translate around the centre of the ellipse
            return new Point(xp + centrePos.X, yp + centrePos.Y);
        }

        private List<Point> ApproximateUnitArc(double theta, double delta)
        {
            var alpha = 4f / 3f * Math.Tan(delta / 4f);

            var x1 = Math.Cos(theta);
            var y1 = Math.Sin(theta);
            var x2 = Math.Cos(theta + delta);
            var y2 = Math.Sin(theta + delta);

            return new List<Point>
            {
                new Point(x1 - y1 * alpha, y1 + x1 * alpha),
                new Point(x2 + y2 * alpha, y2 - x2 * alpha),
                new Point(x2, y2)
            };
        }

        private double UnitVectorAngle(Point u, Point v)
        {
            var sign = (u.X * v.Y - u.Y * v.X < 0) ? -1 : 1;

            var umag = Math.Sqrt(u.X * u.X + u.Y * u.Y);
            var vmag = Math.Sqrt(v.X * v.X + v.Y * v.Y);

            var div = Dot(u, v) / (umag * vmag);

            if (div > 1)
            {
                div = 1;
            }
            if (div < -1)
            {
                div = -1;
            }

            return sign * Math.Acos(div);
        }

        /// <summary>
        ///  This calculates the centre of the arc based on the dimensions provided
        /// </summary>
        /// <param name="p">Start position</param>
        /// <param name="c">End position of the arc</param>
        /// <param name="rx">Radius along x axis</param>
        /// <param name="ry">Radius along w axis</param>
        /// <param name="largeArc">take the long way around</param>
        /// <param name="sweep">take the shorter path</param>
        /// <param name="sinphi">calculated SIN of the rotation</param>
        /// <param name="cosphi">calculated COS of the rotation</param>
        /// <param name="pp">The translated and rotated position of the ellipse</param>
        /// <returns></returns>
        private Arc GetArcCentre(Point p, Point c, double rx, double ry, int largeArc, int sweep, double sinphi, double cosphi, Point pp)
        {
            var result = new Arc();

            var rxsq = rx * rx;
            var rysq = ry * ry;
            var pxpsq = pp.X * pp.X;
            var pypsq = pp.Y * pp.Y;

            var radicand = (rxsq * rysq) - (rxsq * pypsq) - (rysq * pxpsq);
            if (radicand < 0)
            {
                radicand = 0;
            }

            radicand /= (rxsq * pypsq) + (rysq * pxpsq);
            radicand = Math.Sqrt(radicand) * (largeArc == sweep ? -1f : 1f);

            var centrep = new Point()
            {
                X = radicand * rx / ry * pp.Y,
                Y = radicand * -ry / rx * pp.X
            };

            result.Centre.X = (cosphi * centrep.X) - (sinphi * centrep.Y) + (p.X + c.X) / 2f;
            result.Centre.Y = (sinphi * centrep.X) + (cosphi * centrep.Y) + (p.Y + c.Y) / 2f;

            var v1 = new Point()
            {
                X = (pp.X - centrep.X) / rx,
                Y = (pp.Y - centrep.Y) / ry
            };

            var v2 = new Point()
            {
                X = (-pp.X - centrep.X) / rx,
                Y = (-pp.Y - centrep.Y) / ry
            };

            result.Angle1 = UnitVectorAngle(new Point(1, 0), v1);
            result.Angle2 = UnitVectorAngle(v1, v2);

            if (sweep == 0 && result.Angle2 > 0)
            {
                result.Angle2 -= TAU;
            }

            if (sweep == 1 && result.Angle2 < 0)
            {
                result.Angle2 += TAU;
            }

            return result;
        }

        /// <summary>
        /// Converts an svg arc to a cubic bezier
        /// </summary>
        /// <param name="p">The starting position</param>
        /// <param name="c">The ending position</param>
        /// <param name="rx">The radius on the x axis</param>
        /// <param name="ry">The radius on the y axis</param>
        /// <param name="rotation">The rotation of the arc on the x-axis</param>
        /// <param name="largeArc">If the curve is to travel the long way between the two points</param>
        /// <param name="sweep">If the curve is on the reducing angle or the increasing angle</param>
        /// <returns></returns>
        private List<Curve> ArcToBezier(Point p, Point c, double rx, double ry, double rotation = 0, int largeArc = 0, int sweep = 0)
        {
            var result = new List<Curve>();

            // if either radius is zero do nothing
            if (rx == 0 || ry == 0) return result;

            var sinphi = Math.Sin(rotation * TAU / 360f);
            var cosphi = Math.Cos(rotation * TAU / 360f);

            // move the ellipse so that origin (ie 0,0) is at the midpoint between the 
            // start point and the end point. Also rotate this so that the x and y
            // axis are aligned with the grid
            var pp = new Point()
            {
                X = (cosphi * (p.X - c.X) / 2f) + (sinphi * (p.Y - c.Y) / 2f),
                Y = (-sinphi * (p.X - c.X) / 2f) + (cosphi * (p.Y - c.Y) / 2f)
            };

            // If the start and end point of the arc are the same then do nothing
            if (pp.X == 0 && pp.Y == 0) return result;

            rx = Math.Abs(rx);
            ry = Math.Abs(ry);

            // make sure that the radii are big enough. If not then these need
            // to get scaled up to avoid rounding errors in subsequent calculations
            var diff = ((pp.X * pp.X) / (rx * rx)) + ((pp.Y * pp.Y) / (ry * ry));
            if (diff > 1)
            {
                rx *= Math.Sqrt(diff);
                ry *= Math.Sqrt(diff);
            }

            var arc = GetArcCentre(p, c, rx, ry, largeArc, sweep, sinphi, cosphi, pp);

            // split the arc into multiple segments such that each segment covers 
            // at most 90 degrees.
            var segments = Math.Max(Math.Ceiling(Math.Abs(arc.Angle2) / (TAU / 4f)), 1);
            arc.Angle2 /= segments;

            for (var i = 0; i < segments; i++)
            {
                var approx = ApproximateUnitArc(arc.Angle1, arc.Angle2);
                arc.Angle1 += arc.Angle2;

                var cp1 = MapBackToOriginal(approx[0], rx, ry, cosphi, sinphi, arc.Centre);
                var cp2 = MapBackToOriginal(approx[1], rx, ry, cosphi, sinphi, arc.Centre);
                var end = MapBackToOriginal(approx[2], rx, ry, cosphi, sinphi, arc.Centre);

                result.Add(new Curve()
                {
                    Cp1 = cp1,
                    Cp2 = cp2,
                    End = end
                });
            }

            return result;
        }

        private bool PointInRectangle(Point m, Rectangle r)
        {
            var AB = Vector(r.A, r.B);
            var AM = Vector(r.A, m);
            var BC = Vector(r.B, r.C);
            var BM = Vector(r.B, m);
            var dotABAM = Dot(AB, AM);
            var dotABAB = Dot(AB, AB);
            var dotBCBM = Dot(BC, BM);
            var dotBCBC = Dot(BC, BC);
            return 0 <= dotABAM && dotABAM <= dotABAB && 0 <= dotBCBM && dotBCBM <= dotBCBC;
        }

        private Point Vector(Point p1, Point p2)
        {
            return new Point
            {
                X = (p2.X - p1.X),
                Y = (p2.Y - p1.Y)
            };
        }

        private double Dot(Point u, Point v)
        {
            return u.X * v.X + u.Y * v.Y;
        }

    }

}
