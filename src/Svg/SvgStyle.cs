using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace CorePDF.Svg
{
    /// <summary>
    /// The computed values of the presentation properties for an element. Inherited properties
    /// are copied from the parent; the others start from their initial values.
    /// </summary>
    internal sealed class SvgStyle
    {
        // inherited properties
        public string Fill = "black";
        public double FillOpacity = 1;
        public bool FillEvenOdd;
        public string Stroke = "none";
        public string StrokeWidth = "1";
        public double StrokeOpacity = 1;
        public int LineCap; // PDF values: 0 butt, 1 round, 2 square
        public int LineJoin; // PDF values: 0 miter, 1 round, 2 bevel
        public double MiterLimit = 4;
        public string DashArray = "none";
        public string DashOffset = "0";
        public string Color = "black";
        public bool Visible = true;
        public bool ClipEvenOdd;

        // properties that aren't inherited
        public double Opacity = 1;
        public bool Display = true;
        public string ClipPath = "none";
        public string Mask = "none";
        public string Filter = "none";
        public string Marker = "none";
        public string StopColor = "black";
        public double StopOpacity = 1;

        /// <summary>
        /// Properties that aren't inherited, which every element starts again with
        /// </summary>
        public SvgStyle Inherit()
        {
            var child = (SvgStyle)MemberwiseClone();
            child.Opacity = 1;
            child.Display = true;
            child.ClipPath = "none";
            child.Mask = "none";
            child.Filter = "none";
            child.Marker = "none";
            child.StopColor = "black";
            child.StopOpacity = 1;
            return child;
        }

        /// <summary>
        /// The presentation properties that are understood
        /// </summary>
        public static readonly HashSet<string> Properties = new HashSet<string>
        {
            "fill", "fill-opacity", "fill-rule", "stroke", "stroke-width", "stroke-opacity", "stroke-linecap",
            "stroke-linejoin", "stroke-miterlimit", "stroke-dasharray", "stroke-dashoffset", "color",
            "visibility", "clip-rule", "opacity", "display", "clip-path", "mask", "filter",
            "marker", "marker-start", "marker-mid", "marker-end", "stop-color", "stop-opacity"
        };

        /// <summary>
        /// Applies a specified value, eg: ("stroke-width", "2px")
        /// </summary>
        public void Set(string property, string value, SvgStyle parent)
        {
            value = value.Trim();
            if (value == "inherit")
            {
                CopyFrom(property, parent);
                return;
            }

            switch (property)
            {
                case "fill": Fill = value; break;
                case "fill-opacity": FillOpacity = Opacity01(value, FillOpacity); break;
                case "fill-rule": FillEvenOdd = value == "evenodd"; break;
                case "stroke": Stroke = value; break;
                case "stroke-width": StrokeWidth = value; break;
                case "stroke-opacity": StrokeOpacity = Opacity01(value, StrokeOpacity); break;
                case "stroke-linecap": LineCap = value == "round" ? 1 : value == "square" ? 2 : 0; break;
                case "stroke-linejoin": LineJoin = value == "round" ? 1 : value == "bevel" ? 2 : 0; break;
                case "stroke-miterlimit":
                    if (double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var limit) && limit >= 1) MiterLimit = limit;
                    break;
                case "stroke-dasharray": DashArray = value; break;
                case "stroke-dashoffset": DashOffset = value; break;
                case "color": Color = value; break;
                case "visibility": Visible = value == "visible"; break;
                case "clip-rule": ClipEvenOdd = value == "evenodd"; break;
                case "opacity": Opacity = Opacity01(value, Opacity); break;
                case "display": Display = value != "none"; break;
                case "clip-path": ClipPath = value; break;
                case "mask": Mask = value; break;
                case "filter": Filter = value; break;
                case "stop-color": StopColor = value; break;
                case "stop-opacity": StopOpacity = Opacity01(value, StopOpacity); break;
                case "marker":
                case "marker-start":
                case "marker-mid":
                case "marker-end":
                    if (value != "none") Marker = value;
                    break;
            }
        }

        private void CopyFrom(string property, SvgStyle parent)
        {
            switch (property)
            {
                case "fill": Fill = parent.Fill; break;
                case "fill-opacity": FillOpacity = parent.FillOpacity; break;
                case "fill-rule": FillEvenOdd = parent.FillEvenOdd; break;
                case "stroke": Stroke = parent.Stroke; break;
                case "stroke-width": StrokeWidth = parent.StrokeWidth; break;
                case "stroke-opacity": StrokeOpacity = parent.StrokeOpacity; break;
                case "stroke-linecap": LineCap = parent.LineCap; break;
                case "stroke-linejoin": LineJoin = parent.LineJoin; break;
                case "stroke-miterlimit": MiterLimit = parent.MiterLimit; break;
                case "stroke-dasharray": DashArray = parent.DashArray; break;
                case "stroke-dashoffset": DashOffset = parent.DashOffset; break;
                case "color": Color = parent.Color; break;
                case "visibility": Visible = parent.Visible; break;
                case "clip-rule": ClipEvenOdd = parent.ClipEvenOdd; break;
                case "opacity": Opacity = parent.Opacity; break;
                case "display": Display = parent.Display; break;
            }
        }

        /// <summary>
        /// Reads an opacity: a number from 0 to 1 or a percentage
        /// </summary>
        private static double Opacity01(string value, double fallback)
        {
            var percent = value.EndsWith("%");
            if (!double.TryParse(value.TrimEnd('%'), NumberStyles.Float, CultureInfo.InvariantCulture, out var result)) return fallback;
            return Math.Clamp(percent ? result / 100 : result, 0, 1);
        }
    }
}
