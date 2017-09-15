using CorePDF.Pages;
using CorePDF.TypeFaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CorePDF.Contents
{
    public class Shape : Content
    {
        public Polygon Type { get; set; }
        public Stroke Stroke { get; set; } = new Stroke();
        public string FillColor { get; set; }
        public int BorderRadius { get; set; } = 0;

        public override void PrepareStream(Size pageSize, List<Font> fonts, bool compress)
        {
            var roundedness = 0.55191502449m;
            var result = string.Format("{0} w\n", Stroke.Width);
            if (!string.IsNullOrEmpty(Stroke.DashPattern))
            {
                // See page 217 of the PDF Specification for what the second number means
                // but essentially it is the distance into the pattern that the dash is
                // started. 0 means that the first dash begins immediately.
                result += string.Format("[{0}] 0 d\n", Stroke.DashPattern);
            }
            else
            {
                // Solid line .. no dashes
                result += "[] 0 d\n";
            }

            if (!string.IsNullOrEmpty(Stroke.Color))
            {
                result += string.Format("{0} RG\n", ToPDFColor(Stroke.Color));
            }
            else
            {
                result += "0 0 0 RG\n";
            }

            if (!string.IsNullOrEmpty(FillColor))
            {
                result += string.Format("{0} rg\n", ToPDFColor(FillColor));
            }

            // Cubic bezier curves for rounded corners and ellipses
            int curX;   //current X
            int curY;   //current Y
            int x1;     //x1 on Cubic Bezier Curves diagram in manual
            int y1;
            int x2;     //etc, etc
            int y2;
            int x3;     //final position X
            int y3;     //final position Y

            switch (Type)
            {
                case Polygon.Rectangle:
                    if (BorderRadius == 0)
                    {
                        // very simple encoding for rectangles with sharp edges
                        result += string.Format("{0} {1} {2} {3} re\n", PosX, PosY, Width, Height);
                        result += "B";
                    }
                    else
                    {

                        // move to the lower left corner of the rectangle and write the first line vertically
                        // This is calc as x = LLX and y = (LLY+Radius) and is the point where the lower left 
                        // curve moves into the straight line of the box.

                        curX = PosX;
                        curY = PosY + BorderRadius;
                        result += string.Format("{0} {1} m\n", curX.ToString(), curY.ToString());

                        curY = PosY + Height - BorderRadius;
                        result += string.Format("{0} {1} l\n", curX, curY);

                        x1 = curX;
                        y1 = curY + (int)(roundedness * BorderRadius);
                        x2 = curX + BorderRadius - (int)(roundedness * BorderRadius);
                        y2 = curY + BorderRadius;
                        x3 = curX + BorderRadius;
                        y3 = curY + BorderRadius;
                        result += string.Format("{0} {1} {2} {3} {4} {5} c\n", x1.ToString(), y1.ToString(), x2.ToString(), y2.ToString(), x3.ToString(), y3.ToString());

                        curX = PosX + Width - BorderRadius;
                        curY = PosY + Height;
                        result += string.Format("{0} {1} l\n", curX.ToString(), curY.ToString());

                        x1 = PosX + Width - BorderRadius + (int)(BorderRadius * roundedness);
                        y1 = PosY + Height;
                        x2 = PosX + Width;
                        y2 = PosY + Height - BorderRadius + (int)(BorderRadius * roundedness);
                        x3 = PosX + Width;
                        y3 = PosY + Height - BorderRadius;
                        result += string.Format("{0} {1} {2} {3} {4} {5} c\n", x1.ToString(), y1.ToString(), x2.ToString(), y2.ToString(), x3.ToString(), y3.ToString());

                        curX = x3;
                        curY = PosY + BorderRadius;
                        result += string.Format("{0} {1} l\n", curX.ToString(), curY.ToString());

                        x1 = x3;
                        y1 = PosY + BorderRadius - (int)(BorderRadius * roundedness);
                        x2 = PosX + Width - BorderRadius + (int)(BorderRadius * roundedness);
                        y2 = PosY;
                        x3 = PosX + Width - BorderRadius;
                        y3 = PosY;
                        result += string.Format("{0} {1} {2} {3} {4} {5} c\n", x1.ToString(), y1.ToString(), x2.ToString(), y2.ToString(), x3.ToString(), y3.ToString());

                        curX = PosX + BorderRadius;
                        curY = PosY;
                        result += string.Format("{0} {1} l\n", curX.ToString(), curY.ToString());

                        x1 = PosX + BorderRadius - (int)(BorderRadius * roundedness);
                        y1 = PosY;
                        x2 = PosX;
                        y2 = PosY + BorderRadius - (int)(BorderRadius * roundedness);
                        x3 = PosX;
                        y3 = PosY + BorderRadius;
                        result += string.Format("{0} {1} {2} {3} {4} {5} c\n", x1.ToString(), y1.ToString(), x2.ToString(), y2.ToString(), x3.ToString(), y3.ToString());

                        result += "b";
                    }

                    break;
                case Polygon.Line:
                    result += string.Format("{0} {1} m\n", PosX, PosY);
                    result += string.Format("{0} {1} l\n", PosX + Width, PosY + Height);
                    result += "S";

                    break;
                case Polygon.Ellipses:
                    // the coordinates of the ellipses mark the centre of the shape

                    var widthRadius = (Width / 2);
                    var heightRadius = (Height / 2);

                    var posX = PosX - widthRadius;
                    var posY = PosY;

                    result += string.Format("{0} {1} m\n", posX, posY);

                    x1 = posX;
                    y1 = posY + (int)(roundedness * heightRadius);
                    x2 = posX + widthRadius - (int)(roundedness * widthRadius);
                    y2 = posY + heightRadius;
                    x3 = posX + widthRadius;
                    y3 = posY + heightRadius;
                    result += string.Format("{0} {1} {2} {3} {4} {5} c\n", x1.ToString(), y1.ToString(), x2.ToString(), y2.ToString(), x3.ToString(), y3.ToString());

                    x1 = posX + Width - (int)(widthRadius * roundedness);
                    y1 = posY + heightRadius;
                    x2 = posX + Width;
                    y2 = posY + (int)(heightRadius * roundedness);
                    x3 = posX + Width;
                    y3 = posY;
                    result += string.Format("{0} {1} {2} {3} {4} {5} c\n", x1.ToString(), y1.ToString(), x2.ToString(), y2.ToString(), x3.ToString(), y3.ToString());

                    x1 = x3;
                    y1 = posY - (int)(heightRadius * roundedness);
                    x2 = posX + widthRadius + (int)(widthRadius * roundedness);
                    y2 = posY - heightRadius;
                    x3 = posX + widthRadius;
                    y3 = posY - heightRadius;
                    result += string.Format("{0} {1} {2} {3} {4} {5} c\n", x1.ToString(), y1.ToString(), x2.ToString(), y2.ToString(), x3.ToString(), y3.ToString());

                    x1 = posX + widthRadius - (int)(widthRadius * roundedness);
                    y1 = y3;
                    x2 = posX;
                    y2 = posY - (int)(heightRadius * roundedness);
                    x3 = posX;
                    y3 = posY;
                    result += string.Format("{0} {1} {2} {3} {4} {5} c\n", x1.ToString(), y1.ToString(), x2.ToString(), y2.ToString(), x3.ToString(), y3.ToString());

                    result += "b";

                    break;
            }

            _encodedData = Encoding.UTF8.GetBytes(result);

            base.PrepareStream(compress);
        }
    }



}
