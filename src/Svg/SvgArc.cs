using System;
using System.Collections.Generic;

namespace CorePDF.Svg
{
    internal sealed class Point
    {
        public double X { get; set; }
        public double Y { get; set; }

        public Point(double? x = null, double? y = null)
        {
            if (x.HasValue) X = x.Value;
            if (y.HasValue) Y = y.Value;
        }
    }

    internal sealed class Curve
    {
        public Point Cp1 { get; set; } = new Point();
        public Point Cp2 { get; set; } = new Point();
        public Point End { get; set; } = new Point();
    }

    internal sealed class Arc
    {
        public Point Centre { get; set; } = new Point();
        public double Angle1 { get; set; }
        public double Angle2 { get; set; }
    }

    /// <summary>
    /// Converts SVG elliptical arcs into cubic bezier curves (which is all PDF can draw).
    /// Works in SVG co-ordinates: the arc is converted to centre parameterisation, split into
    /// segments of at most 90 degrees and each segment approximated with a cubic curve.
    /// </summary>
    internal static class SvgArc
    {
        const double TAU = Math.PI * 2f;

        /// <summary>
        /// Maps the unit circle co-ordinates back to their original position, rotation and scales
        /// </summary>
        private static Point MapBackToOriginal(Point p, double rx, double ry, double cosphi, double sinphi, Point centrePos)
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

        private static List<Point> ApproximateUnitArc(double theta, double delta)
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

        private static double UnitVectorAngle(Point u, Point v)
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
        private static Arc GetArcCentre(Point p, Point c, double rx, double ry, int largeArc, int sweep, double sinphi, double cosphi, Point pp)
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
        public static List<Curve> ArcToBezier(Point p, Point c, double rx, double ry, double rotation = 0, int largeArc = 0, int sweep = 0)
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

        private static double Dot(Point u, Point v)
        {
            return u.X * v.X + u.Y * v.Y;
        }
    }
}
