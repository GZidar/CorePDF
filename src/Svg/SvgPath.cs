using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace CorePDF.Svg
{
    /// <summary>
    /// Writes PDF path construction operators (m, l, c, h). Points can optionally be transformed
    /// as they are written, which is needed when several shapes with different transforms are
    /// combined into one path (eg: for a clipping path).
    /// </summary>
    internal sealed class SvgPathWriter
    {
        private readonly StringBuilder _output;
        private readonly SvgMatrix _transform;
        private readonly bool _hasTransform;

        public SvgPathWriter(StringBuilder output, SvgMatrix? transform = null)
        {
            _output = output;
            _transform = transform ?? SvgMatrix.Identity;
            _hasTransform = transform.HasValue && !transform.Value.IsIdentity;
        }

        private double _x, _y;
        private double _minX = double.MaxValue, _minY = double.MaxValue, _maxX = double.MinValue, _maxY = double.MinValue;

        /// <summary>
        /// True once anything has been drawn
        /// </summary>
        public bool HasSegments { get; private set; }

        /// <summary>
        /// The exact bounding box of the geometry (before any transform), as
        /// [left, top, right, bottom], or null if nothing has been drawn
        /// </summary>
        public double[] Bounds => _minX > _maxX ? null : new[] { _minX, _minY, _maxX, _maxY };

        public void MoveTo(double x, double y)
        {
            Point(x, y);
            _output.Append("m\n");
            _x = x;
            _y = y;
        }

        public void LineTo(double x, double y)
        {
            Include(_x, _y);
            Include(x, y);
            Point(x, y);
            _output.Append("l\n");
            HasSegments = true;
            _x = x;
            _y = y;
        }

        public void CurveTo(double x1, double y1, double x2, double y2, double x, double y)
        {
            Include(_x, _y);
            Include(x, y);

            // the curve may extend past its end points where its tangent is horizontal or vertical
            foreach (var t in Extremes(_x, x1, x2, x).Concat(Extremes(_y, y1, y2, y)))
            {
                var mt = 1 - t;
                Include(
                    (mt * mt * mt * _x) + (3 * mt * mt * t * x1) + (3 * mt * t * t * x2) + (t * t * t * x),
                    (mt * mt * mt * _y) + (3 * mt * mt * t * y1) + (3 * mt * t * t * y2) + (t * t * t * y));
            }

            Point(x1, y1);
            Point(x2, y2);
            Point(x, y);
            _output.Append("c\n");
            HasSegments = true;
            _x = x;
            _y = y;
        }

        private void Include(double x, double y)
        {
            _minX = Math.Min(_minX, x);
            _minY = Math.Min(_minY, y);
            _maxX = Math.Max(_maxX, x);
            _maxY = Math.Max(_maxY, y);
        }

        /// <summary>
        /// The parameter values (between 0 and 1) where one co-ordinate of a cubic curve has a
        /// turning point: the roots of its derivative
        /// </summary>
        private static IEnumerable<double> Extremes(double p0, double p1, double p2, double p3)
        {
            var a = -p0 + (3 * p1) - (3 * p2) + p3;
            var b = 2 * (p0 - (2 * p1) + p2);
            var c = p1 - p0;

            if (Math.Abs(a) < 1e-12)
            {
                if (Math.Abs(b) > 1e-12)
                {
                    var t = -c / b;
                    if (t > 0 && t < 1) yield return t;
                }
                yield break;
            }

            var discriminant = (b * b) - (4 * a * c);
            if (discriminant < 0) yield break;

            var root = Math.Sqrt(discriminant);
            foreach (var t in new[] { (-b + root) / (2 * a), (-b - root) / (2 * a) })
            {
                if (t > 0 && t < 1) yield return t;
            }
        }

        public void Close()
        {
            _output.Append("h\n");
        }

        private void Point(double x, double y)
        {
            if (_hasTransform)
            {
                (x, y) = _transform.Apply(x, y);
            }
            _output.Append(SvgNumber.Format(x)).Append(' ').Append(SvgNumber.Format(y)).Append(' ');
        }
    }

    /// <summary>
    /// Reads SVG path data (the "d" attribute) following the grammar in the SVG specification:
    /// absolute and relative commands, implicit repeated commands, compact number formats
    /// ("10-5", ".5.5", "1e-3") and arc flags written without separators.
    /// </summary>
    internal static class SvgPathData
    {
        public static void Write(string data, SvgPathWriter writer)
        {
            if (string.IsNullOrWhiteSpace(data)) return;

            var pos = 0;
            var command = '\0';
            double x = 0, y = 0; // current point
            double startX = 0, startY = 0; // start of the current subpath
            double lastControlX = 0, lastControlY = 0; // for the smooth curve commands
            var previous = '\0';
            var needsMove = false; // set after a close path that isn't followed by a move

            void EnsureSubpath()
            {
                if (needsMove)
                {
                    writer.MoveTo(x, y);
                    needsMove = false;
                }
            }

            while (true)
            {
                while (pos < data.Length && (char.IsWhiteSpace(data[pos]) || data[pos] == ',')) pos++;
                if (pos >= data.Length) break;

                var c = data[pos];
                if (char.IsLetter(c) && c != 'e' && c != 'E')
                {
                    command = c;
                    pos++;

                    if (command == 'Z' || command == 'z')
                    {
                        writer.Close();
                        x = startX;
                        y = startY;
                        needsMove = true;
                        previous = command;
                        continue;
                    }
                }
                else if (command == '\0' || command == 'Z' || command == 'z')
                {
                    // numbers with no command (or after a close path) are an error: stop drawing here
                    break;
                }

                var relative = char.IsLower(command);
                double ox = relative ? x : 0, oy = relative ? y : 0;

                // reads the given number of arguments, or returns null if they aren't all there
                double[] Args(int count)
                {
                    var values = new double[count];
                    for (var i = 0; i < count; i++)
                    {
                        SvgNumber.SkipSeparators(data, ref pos);
                        if (!SvgNumber.TryRead(data, ref pos, out values[i])) return null;
                    }
                    return values;
                }

                // arc flags are single characters that don't need separating ("a5 5 0 1010 0")
                int Flag()
                {
                    SvgNumber.SkipSeparators(data, ref pos);
                    if (pos < data.Length && (data[pos] == '0' || data[pos] == '1'))
                    {
                        return data[pos++] - '0';
                    }
                    return -1;
                }

                var ok = true;
                double[] a;
                switch (char.ToUpperInvariant(command))
                {
                    case 'M':
                        if ((a = Args(2)) == null) { ok = false; break; }
                        x = ox + a[0];
                        y = oy + a[1];
                        writer.MoveTo(x, y);
                        startX = x;
                        startY = y;
                        needsMove = false;
                        // further pairs of numbers are treated as line to commands
                        command = relative ? 'l' : 'L';
                        break;

                    case 'L':
                        if ((a = Args(2)) == null) { ok = false; break; }
                        EnsureSubpath();
                        x = ox + a[0];
                        y = oy + a[1];
                        writer.LineTo(x, y);
                        break;

                    case 'H':
                        if ((a = Args(1)) == null) { ok = false; break; }
                        EnsureSubpath();
                        x = ox + a[0];
                        writer.LineTo(x, y);
                        break;

                    case 'V':
                        if ((a = Args(1)) == null) { ok = false; break; }
                        EnsureSubpath();
                        y = oy + a[0];
                        writer.LineTo(x, y);
                        break;

                    case 'C':
                        if ((a = Args(6)) == null) { ok = false; break; }
                        EnsureSubpath();
                        writer.CurveTo(ox + a[0], oy + a[1], ox + a[2], oy + a[3], ox + a[4], oy + a[5]);
                        lastControlX = ox + a[2];
                        lastControlY = oy + a[3];
                        x = ox + a[4];
                        y = oy + a[5];
                        break;

                    case 'S':
                    {
                        if ((a = Args(4)) == null) { ok = false; break; }
                        EnsureSubpath();
                        // the first control point is the reflection of the previous curve's second one
                        var smooth = "CcSs".IndexOf(previous) >= 0;
                        var x1 = smooth ? (2 * x) - lastControlX : x;
                        var y1 = smooth ? (2 * y) - lastControlY : y;
                        writer.CurveTo(x1, y1, ox + a[0], oy + a[1], ox + a[2], oy + a[3]);
                        lastControlX = ox + a[0];
                        lastControlY = oy + a[1];
                        x = ox + a[2];
                        y = oy + a[3];
                        break;
                    }

                    case 'Q':
                        if ((a = Args(4)) == null) { ok = false; break; }
                        EnsureSubpath();
                        Quadratic(writer, x, y, ox + a[0], oy + a[1], ox + a[2], oy + a[3]);
                        lastControlX = ox + a[0];
                        lastControlY = oy + a[1];
                        x = ox + a[2];
                        y = oy + a[3];
                        break;

                    case 'T':
                    {
                        if ((a = Args(2)) == null) { ok = false; break; }
                        EnsureSubpath();
                        var smooth = "QqTt".IndexOf(previous) >= 0;
                        var qx = smooth ? (2 * x) - lastControlX : x;
                        var qy = smooth ? (2 * y) - lastControlY : y;
                        Quadratic(writer, x, y, qx, qy, ox + a[0], oy + a[1]);
                        lastControlX = qx;
                        lastControlY = qy;
                        x = ox + a[0];
                        y = oy + a[1];
                        break;
                    }

                    case 'A':
                    {
                        var radii = Args(3);
                        var largeArc = radii == null ? -1 : Flag();
                        var sweep = largeArc < 0 ? -1 : Flag();
                        var end = sweep < 0 ? null : Args(2);
                        if (end == null) { ok = false; break; }

                        EnsureSubpath();
                        var endX = ox + end[0];
                        var endY = oy + end[1];

                        if (radii[0] == 0 || radii[1] == 0)
                        {
                            // an arc with no radius is a straight line
                            writer.LineTo(endX, endY);
                        }
                        else
                        {
                            foreach (var curve in SvgArc.ArcToBezier(new Point(x, y), new Point(endX, endY), radii[0], radii[1], radii[2], largeArc, sweep))
                            {
                                writer.CurveTo(curve.Cp1.X, curve.Cp1.Y, curve.Cp2.X, curve.Cp2.Y, curve.End.X, curve.End.Y);
                            }
                        }
                        x = endX;
                        y = endY;
                        break;
                    }

                    default:
                        // an unknown command: stop drawing here
                        return;
                }

                if (!ok) return;
                previous = command;
            }
        }

        /// <summary>
        /// Draws a quadratic curve as the equivalent cubic curve
        /// </summary>
        private static void Quadratic(SvgPathWriter writer, double x0, double y0, double qx, double qy, double x, double y)
        {
            writer.CurveTo(
                x0 + (2.0 / 3 * (qx - x0)), y0 + (2.0 / 3 * (qy - y0)),
                x + (2.0 / 3 * (qx - x)), y + (2.0 / 3 * (qy - y)),
                x, y);
        }

        /// <summary>
        /// The control point distance for approximating a quarter circle with a cubic curve
        /// </summary>
        private const double KAPPA = 0.5522847498307936;

        public static void Ellipse(SvgPathWriter writer, double cx, double cy, double rx, double ry)
        {
            var kx = rx * KAPPA;
            var ky = ry * KAPPA;
            writer.MoveTo(cx + rx, cy);
            writer.CurveTo(cx + rx, cy + ky, cx + kx, cy + ry, cx, cy + ry);
            writer.CurveTo(cx - kx, cy + ry, cx - rx, cy + ky, cx - rx, cy);
            writer.CurveTo(cx - rx, cy - ky, cx - kx, cy - ry, cx, cy - ry);
            writer.CurveTo(cx + kx, cy - ry, cx + rx, cy - ky, cx + rx, cy);
            writer.Close();
        }

        public static void Rectangle(SvgPathWriter writer, double x, double y, double width, double height, double rx, double ry)
        {
            if (rx <= 0 || ry <= 0)
            {
                writer.MoveTo(x, y);
                writer.LineTo(x + width, y);
                writer.LineTo(x + width, y + height);
                writer.LineTo(x, y + height);
                writer.Close();
                return;
            }

            var kx = rx * KAPPA;
            var ky = ry * KAPPA;
            writer.MoveTo(x + rx, y);
            writer.LineTo(x + width - rx, y);
            writer.CurveTo(x + width - rx + kx, y, x + width, y + ry - ky, x + width, y + ry);
            writer.LineTo(x + width, y + height - ry);
            writer.CurveTo(x + width, y + height - ry + ky, x + width - rx + kx, y + height, x + width - rx, y + height);
            writer.LineTo(x + rx, y + height);
            writer.CurveTo(x + rx - kx, y + height, x, y + height - ry + ky, x, y + height - ry);
            writer.LineTo(x, y + ry);
            writer.CurveTo(x, y + ry - ky, x + rx - kx, y, x + rx, y);
            writer.Close();
        }
    }
}
