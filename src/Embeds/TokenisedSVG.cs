using System;
using System.Collections.Generic;
using System.Text;

namespace CorePDF.Embeds
{
    public class TokenisedSVG
    {
        public List<PDFPath> Paths { get; set; } = new List<PDFPath>();
    }

    public class PDFPath
    {
        public string Command { get; set; }
        public List<PDFPathParam> Parameters { get; set; }

        public PDFPath(string command, List<PDFPathParam> args = null)
        {
            Command = command;

            Parameters = args;
        }
    }

    public class PDFPathParam
    {
        public decimal Value { get; set; }
        public string Operation { get; set; }
    }

    public class Arc
    {
        public Point Centre { get; set; } = new Point();
        public double Angle1 { get; set; }
        public double Angle2 { get; set; }
    }

    public class Curve
    {
        public Point Cp1 { get; set; } = new Point();
        public Point Cp2 { get; set; } = new Point();
        public Point End { get; set; } = new Point();
    }

    public class Rectangle
    {
        public Point A { get; set; } = new Point();
        public Point B { get; set; } = new Point();
        public Point C { get; set; } = new Point();
        public Point D { get; set; } = new Point();
    }

    public class Point
    {

        public double X { get; set; }
        public double Y { get; set; }

        public Point(double? x = null, double? y = null)
        {
            if (x.HasValue)
            {
                X = x.Value;
            }
            if (y.HasValue)
            {
                Y = y.Value;
            }
        }
    }
}
