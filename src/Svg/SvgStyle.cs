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
        public string FontFamily = "serif";
        public double FontSize = 16;
        public bool Bold;
        public bool Italic;
        public string TextAnchor = "start";
        public string LetterSpacing = "normal";
        public string WordSpacing = "normal";
        public string DominantBaseline = "auto";

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
            "marker", "marker-start", "marker-mid", "marker-end", "stop-color", "stop-opacity",
            "font-family", "font-size", "font-weight", "font-style", "text-anchor", "letter-spacing",
            "word-spacing", "dominant-baseline"
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
                case "font-family": FontFamily = value; break;
                case "font-size": FontSize = ParseFontSize(value, parent.FontSize); break;
                case "font-weight":
                    Bold = value == "bold" || value == "bolder" || (int.TryParse(value, out var weight) && weight >= 600);
                    break;
                case "font-style": Italic = value == "italic" || value.StartsWith("oblique"); break;
                case "text-anchor": TextAnchor = value; break;
                case "letter-spacing": LetterSpacing = value; break;
                case "word-spacing": WordSpacing = value; break;
                case "dominant-baseline": DominantBaseline = value; break;
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
                case "font-family": FontFamily = parent.FontFamily; break;
                case "font-size": FontSize = parent.FontSize; break;
                case "font-weight": Bold = parent.Bold; break;
                case "font-style": Italic = parent.Italic; break;
                case "text-anchor": TextAnchor = parent.TextAnchor; break;
                case "letter-spacing": LetterSpacing = parent.LetterSpacing; break;
                case "word-spacing": WordSpacing = parent.WordSpacing; break;
                case "dominant-baseline": DominantBaseline = parent.DominantBaseline; break;
            }
        }

        private static readonly Dictionary<string, double> FontSizeKeywords = new Dictionary<string, double>
        {
            { "xx-small", 9 }, { "x-small", 10 }, { "small", 13 }, { "medium", 16 },
            { "large", 18 }, { "x-large", 24 }, { "xx-large", 32 }, { "xxx-large", 48 }
        };

        /// <summary>
        /// Reads a font size in CSS pixels. Relative sizes (em, %, larger, smaller) are relative
        /// to the parent's font size.
        /// </summary>
        private static double ParseFontSize(string value, double parentSize)
        {
            value = value.Trim().ToLowerInvariant();
            if (FontSizeKeywords.TryGetValue(value, out var keyword)) return keyword;
            if (value == "larger") return parentSize * 1.2;
            if (value == "smaller") return parentSize / 1.2;

            var pos = 0;
            if (!SvgNumber.TryRead(value, ref pos, out var number) || number < 0) return parentSize;

            switch (value.Substring(pos))
            {
                case "":
                case "px": return number;
                case "pt": return number * 96 / 72;
                case "pc": return number * 16;
                case "mm": return number * 96 / 25.4;
                case "cm": return number * 96 / 2.54;
                case "in": return number * 96;
                case "em":
                case "rem": return number * parentSize;
                case "ex": return number * parentSize / 2;
                case "%": return number * parentSize / 100;
                default: return parentSize;
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
