using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using Xunit;

namespace CorePDF.UnitTests
{
    public class TokenisedSVG
    {
        public List<PDFPath> Paths { get; set; } = new List<PDFPath>();
    }

    public class PDFPath
    {
        public string Command { get; set; }
        public object[] Parameters { get; set; }

        public PDFPath(string command, params object[] args)
        {
            Command = command;

            if (args != null)
            {
                Parameters = args;
            }
        }
    }

    [Trait("Category", "Unit Test")]
    public class ConversionTests
    {
        [Fact]
        public void BasicSVGPath_ExpectSuccess()
        {
            //var sourceData = "<svg height='210' width='400'><path d='M150 0 L75 200 L225 200 Z' /></svg>";
            //using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(sourceData)))
            //{
            //    var sourceSVG = new XPathDocument(memStream);
            //    var nav = sourceSVG.CreateNavigator();
            //    if (nav.MoveToChild("svg", ""))
            //    {
            //        decimal.TryParse(nav.GetAttribute("width", ""), out decimal width);
            //        decimal.TryParse(nav.GetAttribute("height", ""), out decimal height);

            //        var paths = nav.SelectChildren("path", "");
            //        var result = new TokenisedSVG();

            //        while (paths.MoveNext())
            //        {
            //            var path = paths.Current.GetAttribute("d", "");
            //            var fill = paths.Current.GetAttribute("fill", "");
            //            var strokeColor = paths.Current.GetAttribute("stroke", "");
            //            if (!string.IsNullOrEmpty(strokeColor))
            //            {
            //                result.Paths.Add(new PDFPath("{0} w\n", ToPDFColor(strokeColor)));
            //            }

            //            var strokeWidth = 1m;
            //            if (!string.IsNullOrEmpty(paths.Current.GetAttribute("stroke-width", "")))
            //            {
            //                decimal.TryParse(paths.Current.GetAttribute("stroke-width", ""), out strokeWidth);
            //            }
            //            result.Paths.Add(new PDFPath("{0} w\n", strokeWidth));

            //            // interpret the path
            //            path = path.Replace(",", " ");
            //            path = path.Replace("M", "M "); // Move To
            //            path = path.Replace("m", "m ");
            //            path = path.Replace("Z", "Z "); // Closepath
            //            path = path.Replace("z", "z ");
            //            path = path.Replace("L", "L "); // Line
            //            path = path.Replace("l", "l ");
            //            path = path.Replace("H", "H "); // Horizontal
            //            path = path.Replace("h", "h ");
            //            path = path.Replace("V", "V "); // Vertical
            //            path = path.Replace("v", "v ");
            //            path = path.Replace("C", "C "); // Cubic Bezier curve
            //            path = path.Replace("c", "c ");
            //            path = path.Replace("S", "S "); // Smoothed CBC
            //            path = path.Replace("s", "s ");
            //            path = path.Replace("Q", "Q "); // Quadratic Bezier curve
            //            path = path.Replace("q", "q ");
            //            path = path.Replace("T", "T "); // Smoothed QBC
            //            path = path.Replace("t", "t ");
            //            path = path.Replace("A", "A "); // Eliptical Arc
            //            path = path.Replace("a", "a ");

            //            var pathElements = path.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            //            var position = 0;
            //            var posX = 0m;
            //            var posY = 0m;
            //            var startPosX = 0m;
            //            var startPosY = 0m;
            //            var inLineMode = false;

            //            while (position < pathElements.Length)
            //            {
            //                var element = pathElements[position];
            //                switch (element)
            //                {
            //                    case "M":
            //                    case "m":
            //                        // execute the move-to command
            //                        position++;
            //                        posX = decimal.Parse(pathElements[position]);
            //                        position++;
            //                        posY = height - decimal.Parse(pathElements[position]);

            //                        result.Paths.Add(new PDFPath("{0} {1} m\n", posX, posY));

            //                        startPosX = posX;
            //                        startPosY = posY;
            //                        inLineMode = false;

            //                        break;
            //                    case "Z":
            //                    case "z":
            //                        // execute the close-path command
            //                        result.Paths.Add(new PDFPath("{0} {1} l\n", startPosX, startPosY));
            //                        inLineMode = false;

            //                        break;
            //                    case "L":
            //                    case "l":
            //                        // execute the line-to command
            //                        position++;
            //                        posX = decimal.Parse(pathElements[position]);
            //                        position++;
            //                        posY = height - decimal.Parse(pathElements[position]);

            //                        result.Paths.Add(new PDFPath("{0} {1} l\n", posX, posY));
            //                        inLineMode = true;

            //                        break;
            //                    case "H":
            //                    case "h":
            //                        // execute the line-to command
            //                        position++;
            //                        posX = decimal.Parse(pathElements[position]);

            //                        result.Paths.Add(new PDFPath("{0} {1} l\n", posX, posY));
            //                        inLineMode = true;

            //                        break;
            //                    case "V":
            //                    case "v":
            //                        // execute the line-to command
            //                        position++;
            //                        posY = height - decimal.Parse(pathElements[position]);

            //                        result.Paths.Add(new PDFPath("{0} {1} l\n", posX, posY));
            //                        inLineMode = true;
            //                        break;

            //                    case "C":
            //                    case "c":
            //                        // execute the cubic bezier command
            //                        position++;
            //                        var cx1 = decimal.Parse(pathElements[position]);
            //                        position++;
            //                        var cy1 = height - decimal.Parse(pathElements[position]);

            //                        position++;
            //                        var cx2 = decimal.Parse(pathElements[position]);
            //                        position++;
            //                        var cy2 = height - decimal.Parse(pathElements[position]);

            //                        position++;
            //                        posX = decimal.Parse(pathElements[position]);
            //                        position++;
            //                        posY = height - decimal.Parse(pathElements[position]);

            //                        result.Paths.Add(new PDFPath("{0} {1} {2} {3} {4} {5} c\n", cx1, cy1, cx2, cy2, posX, posY));
            //                        break;

            //                    case "Q":
            //                    case "q":
            //                        // execute the quadratic bezier command
            //                        position++;
            //                        var qx1 = decimal.Parse(pathElements[position]);
            //                        position++;
            //                        var qy1 = height - decimal.Parse(pathElements[position]);

            //                        // need to calculate the first cubic control points from start position 
            //                        // and the quadratic control point
            //                        var x1 = posX + (2 / 3 * (qx1 - posX));
            //                        var y1 = posY + (2 / 3 * (qy1 - posY));

            //                        position++;
            //                        posX = decimal.Parse(pathElements[position]);
            //                        position++;
            //                        posY = height - decimal.Parse(pathElements[position]);

            //                        // need to calculate the second cubic control points from end position 
            //                        // and the quadratic control point
            //                        var x2 = posX + (2 / 3 * (qx1 - posX));
            //                        var y2 = posY + (2 / 3 * (qy1 - posY));

            //                        result.Paths.Add(new PDFPath("{0} {1} {2} {3} {4} {5} c\n", x1, y1, x2, y2, posX, posY));
            //                        break;

            //                    default:
            //                        // not a command so it must be a set of coordinates
            //                        if (inLineMode)
            //                        {
            //                            posX = decimal.Parse(pathElements[position]);
            //                            position++;
            //                            posY = height - decimal.Parse(pathElements[position]);
            //                            result.Paths.Add(new PDFPath("{0} {1} l\n", posX, posY));
            //                        }
            //                        break;
            //                }

            //                position++;
            //            }
            //        }

            //        var fred = 1;
            //    }
            //}
        }
    }
}
