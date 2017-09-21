using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml.XPath;
using Xunit;

namespace CorePDF.UnitTests
{
    [Trait("Category", "Unit Test")]
    public class ConversionTests
    {
        [Fact]
        public void BasicSVGPath_ExpectSuccess()
        {
            var sourceData = "<svg height='210' width='400'><path d='M150 0 L75 200 L225 200 Z' /></svg>";
            using (var memStream = new MemoryStream(Encoding.UTF8.GetBytes(sourceData)))
            {
                var sourceSVG = new XPathDocument(memStream);
                var nav = sourceSVG.CreateNavigator();
                if (nav.MoveToChild("svg", ""))
                {
                    var width = nav.GetAttribute("width", "");
                    var height = nav.GetAttribute("height", "");

                    var paths = nav.SelectChildren("path", "");
                    var result = "";

                    while (paths.MoveNext())
                    {
                        var path = paths.Current.GetAttribute("d", "");
                        var fill = paths.Current.GetAttribute("fill", "");
                        var strokeColor = paths.Current.GetAttribute("stroke", "");

                        var strokeWidth = "1";
                        if (!string.IsNullOrEmpty(paths.Current.GetAttribute("stroke-width", "")))
                        {
                            strokeWidth = paths.Current.GetAttribute("stroke-width", "");
                        }
                        result += string.Format("{0} w\n", strokeWidth);

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
                        path = path.Replace("S", "S "); // Smoothed CBC
                        path = path.Replace("s", "s ");
                        path = path.Replace("Q", "Q "); // Quadratic Bezier curve
                        path = path.Replace("q", "q ");
                        path = path.Replace("T", "T "); // Smoothed QBC
                        path = path.Replace("t", "t ");
                        path = path.Replace("A", "A "); // Eliptical Arc
                        path = path.Replace("a", "a ");

                        var pathElements = path.Split(' ', StringSplitOptions.RemoveEmptyEntries);
                        var position = 0;
                        var posX = "";
                        var posY = "";
                        var startPosX = "";
                        var startPosY = "";
                        var inLineMode = false;
                        while (position < pathElements.Length)
                        {
                            var element = pathElements[position];
                            switch (element)
                            {
                                case "M":
                                case "m":
                                    // execute the move-to command
                                    position++;
                                    posX = pathElements[position];
                                    position++;
                                    posY = pathElements[position];

                                    result += string.Format("{0} {1} m\n", posX, posY);

                                    startPosX = posX;
                                    startPosY = posY;
                                    inLineMode = false;

                                    break;
                                case "Z":
                                case "z":
                                    // execute the close-path command
                                    result += string.Format("{0} {1} l\n", startPosX, startPosY);
                                    inLineMode = false;

                                    break;
                                case "L":
                                case "l":
                                    // execute the line-to command
                                    position++;
                                    posX = pathElements[position];
                                    position++;
                                    posY = pathElements[position];

                                    result += string.Format("{0} {1} l\n", posX, posY);
                                    inLineMode = true;

                                    break;
                                case "H":
                                case "h":
                                    // execute the line-to command
                                    position++;
                                    posX = pathElements[position];

                                    result += string.Format("{0} {1} l\n", posX, posY);
                                    inLineMode = true;

                                    break;
                                case "V":
                                case "v":
                                    // execute the line-to command
                                    position++;
                                    posY = pathElements[position];

                                    result += string.Format("{0} {1} l\n", posX, posY);
                                    inLineMode = true;
                                    break;
                                default:
                                    // not a command so it must be a set of coordinates
                                    if (inLineMode)
                                    {
                                        posX = pathElements[position];
                                        position++;
                                        posY = pathElements[position];
                                        result += string.Format("{0} {1} l\n", posX, posY);
                                    }
                                    break;
                            }

                            position++;
                        }
                    }

                    var fred = 1;
                }
            }
        }
    }
}
