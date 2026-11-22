using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CorePDF.Svg
{
    /// <summary>
    /// A resolved linear or radial gradient (with any attributes and stops it inherits through
    /// href resolved). Co-ordinates are in the gradient's own space: fractions of the shape's
    /// bounding box for objectBoundingBox units, otherwise user space.
    /// </summary>
    internal sealed class SvgGradient
    {
        public bool Radial { get; set; }
        public bool ObjectBoundingBox { get; set; } = true;
        public SvgMatrix Transform { get; set; } = SvgMatrix.Identity;
        public string Spread { get; set; } = "pad";

        // linear gradients: the start and end of the gradient vector
        public double X1, Y1, X2, Y2;

        // radial gradients: the end circle (cx, cy, r) and the focal (start) circle (fx, fy, fr)
        public double Cx, Cy, R, Fx, Fy, Fr;

        /// <summary>
        /// The stops, in order, with offsets from 0 to 1. Colours include the stop's opacity.
        /// </summary>
        public List<(double Offset, SvgColor Color)> Stops { get; } = new List<(double, SvgColor)>();

        /// <summary>
        /// True if the gradient can't vary, so is drawn with the colour of its last stop (as the
        /// specification requires for a zero length vector or zero radius)
        /// </summary>
        public bool IsSolid => Stops.Count == 1
            || (!Radial && X1 == X2 && Y1 == Y2)
            || (Radial && R <= 0);

        /// <summary>
        /// True if every stop has the same opacity, so it can be applied to the whole shape
        /// rather than needing a soft mask
        /// </summary>
        public bool UniformOpacity => Stops.All(s => Math.Abs(s.Color.Alpha - Stops[0].Color.Alpha) < 0.001);
    }

    /// <summary>
    /// Builds the PDF shading dictionaries that draw gradients: axial (type 2) shadings for
    /// linear gradients and radial (type 3) shadings for radial ones, with a stitching function
    /// joining linear interpolations between the stops.
    /// </summary>
    internal static class SvgShading
    {
        /// <summary>
        /// The most times a repeating or reflecting gradient is repeated across a shape
        /// </summary>
        private const int MAXPERIODS = 256;

        /// <param name="gradient">the gradient</param>
        /// <param name="alpha">true to build the opacity of the stops (as grey) instead of their colour</param>
        /// <param name="bounds">the area the gradient has to cover, in gradient space: [x0, y0, x1, y1]</param>
        public static string Build(SvgGradient gradient, bool alpha, double[] bounds)
        {
            var stops = gradient.Stops
                .Select(s => (s.Offset, alpha ? new[] { s.Color.Alpha } : new[] { s.Color.R, s.Color.G, s.Color.B }))
                .ToList();
            var function = StopFunction(stops);

            // repeating and reflecting gradients are drawn by repeating the function over as
            // many periods as are needed to cover the shape
            var first = 0;
            var last = 1;
            if (gradient.Spread == "repeat" || gradient.Spread == "reflect")
            {
                (first, last) = Periods(gradient, bounds);
                if (last - first > 1 || first != 0)
                {
                    function = Repeat(function, first, last, gradient.Spread == "reflect");
                }
            }

            string coords;
            if (gradient.Radial)
            {
                // circles grow linearly from the focal circle (t = 0) to the end circle (t = 1)
                coords = Numbers(gradient.Fx, gradient.Fy, gradient.Fr,
                    gradient.Fx + (last * (gradient.Cx - gradient.Fx)),
                    gradient.Fy + (last * (gradient.Cy - gradient.Fy)),
                    gradient.Fr + (last * (gradient.R - gradient.Fr)));
            }
            else
            {
                var dx = gradient.X2 - gradient.X1;
                var dy = gradient.Y2 - gradient.Y1;
                coords = Numbers(gradient.X1 + (first * dx), gradient.Y1 + (first * dy), gradient.X1 + (last * dx), gradient.Y1 + (last * dy));
            }

            return string.Format("<< /ShadingType {0} /ColorSpace /{1} /Coords [{2}] /Domain [{3} {4}] /Function {5} /Extend [true true] >>",
                gradient.Radial ? 3 : 2, alpha ? "DeviceGray" : "DeviceRGB", coords, first, last, function);
        }

        /// <summary>
        /// A function from 0 to 1 that interpolates linearly between the stops (and keeps the
        /// first and last colours before the first stop and after the last)
        /// </summary>
        private static string StopFunction(List<(double Offset, double[] Values)> stops)
        {
            var pieces = new List<(double From, double To, double[] Start, double[] End)>();

            if (stops[0].Offset > 0)
            {
                pieces.Add((0, stops[0].Offset, stops[0].Values, stops[0].Values));
            }
            for (var i = 0; i + 1 < stops.Count; i++)
            {
                // stops at the same offset make a sharp change, which happens at the boundary
                if (stops[i + 1].Offset > stops[i].Offset)
                {
                    pieces.Add((stops[i].Offset, stops[i + 1].Offset, stops[i].Values, stops[i + 1].Values));
                }
            }
            var lastStop = stops[stops.Count - 1];
            if (lastStop.Offset < 1)
            {
                pieces.Add((lastStop.Offset, 1, lastStop.Values, lastStop.Values));
            }
            if (pieces.Count == 0)
            {
                pieces.Add((0, 1, lastStop.Values, lastStop.Values));
            }

            var functions = string.Join(" ", pieces.Select(p =>
                string.Format("<< /FunctionType 2 /Domain [0 1] /C0 [{0}] /C1 [{1}] /N 1 >>", Numbers(p.Start), Numbers(p.End))));
            var bounds = Numbers(pieces.Skip(1).Select(p => p.From).ToArray());
            var encode = string.Join(" ", pieces.Select(_ => "0 1"));

            return string.Format("<< /FunctionType 3 /Domain [0 1] /Functions [{0}] /Bounds [{1}] /Encode [{2}] >>", functions, bounds, encode);
        }

        /// <summary>
        /// Repeats a function from 0 to 1 over the periods from first to last, reversing every
        /// other period for a reflected gradient
        /// </summary>
        private static string Repeat(string function, int first, int last, bool reflect)
        {
            var count = last - first;
            var functions = string.Join(" ", Enumerable.Repeat(function, count));
            var bounds = Numbers(Enumerable.Range(first + 1, count - 1).Select(b => (double)b).ToArray());
            var encode = string.Join(" ", Enumerable.Range(first, count).Select(period => reflect && Math.Abs(period) % 2 == 1 ? "1 0" : "0 1"));

            return string.Format("<< /FunctionType 3 /Domain [{0} {1}] /Functions [{2}] /Bounds [{3}] /Encode [{4}] >>", first, last, functions, bounds, encode);
        }

        /// <summary>
        /// The range of whole periods of the gradient needed to cover the area
        /// </summary>
        private static (int First, int Last) Periods(SvgGradient gradient, double[] bounds)
        {
            var corners = new[] { (bounds[0], bounds[1]), (bounds[2], bounds[1]), (bounds[0], bounds[3]), (bounds[2], bounds[3]) };

            if (gradient.Radial)
            {
                // how far the area extends from the focal point, and how quickly the circles grow
                var reach = corners.Max(c => Math.Sqrt(Math.Pow(c.Item1 - gradient.Fx, 2) + Math.Pow(c.Item2 - gradient.Fy, 2)));
                var growth = (gradient.R - gradient.Fr) - Math.Sqrt(Math.Pow(gradient.Cx - gradient.Fx, 2) + Math.Pow(gradient.Cy - gradient.Fy, 2));
                if (growth <= 0) return (0, 1);

                return (0, Math.Clamp((int)Math.Ceiling((reach - gradient.Fr) / growth), 1, MAXPERIODS));
            }

            var dx = gradient.X2 - gradient.X1;
            var dy = gradient.Y2 - gradient.Y1;
            var length = (dx * dx) + (dy * dy);
            var ts = corners.Select(c => (((c.Item1 - gradient.X1) * dx) + ((c.Item2 - gradient.Y1) * dy)) / length).ToList();

            var first = (int)Math.Floor(ts.Min());
            var last = (int)Math.Ceiling(ts.Max());
            if (last <= first) last = first + 1;
            if (last - first > MAXPERIODS)
            {
                first = Math.Max(first, -MAXPERIODS / 2);
                last = first + MAXPERIODS;
            }
            return (first, last);
        }

        private static string Numbers(params double[] values)
        {
            return string.Join(" ", values.Select(SvgNumber.Format));
        }
    }
}
