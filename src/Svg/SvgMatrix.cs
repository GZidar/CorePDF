using System;
using System.Collections.Generic;
using System.Globalization;

namespace CorePDF.Svg
{
    /// <summary>
    /// A 2D affine transformation, using the same layout as SVG and PDF: [a b c d e f] maps
    /// (x, y) to (a*x + c*y + e, b*x + d*y + f).
    /// </summary>
    internal readonly struct SvgMatrix
    {
        public readonly double A, B, C, D, E, F;

        public SvgMatrix(double a, double b, double c, double d, double e, double f)
        {
            A = a; B = b; C = c; D = d; E = e; F = f;
        }

        public static readonly SvgMatrix Identity = new SvgMatrix(1, 0, 0, 1, 0, 0);

        public bool IsIdentity => A == 1 && B == 0 && C == 0 && D == 1 && E == 0 && F == 0;

        public static SvgMatrix Translate(double x, double y) => new SvgMatrix(1, 0, 0, 1, x, y);

        public static SvgMatrix Scale(double x, double y) => new SvgMatrix(x, 0, 0, y, 0, 0);

        public static SvgMatrix Rotate(double degrees)
        {
            var radians = degrees * Math.PI / 180;
            var cos = Math.Cos(radians);
            var sin = Math.Sin(radians);
            return new SvgMatrix(cos, sin, -sin, cos, 0, 0);
        }

        /// <summary>
        /// Applies <paramref name="inner"/> first and then this matrix (the order transforms are
        /// nested in SVG: the parent's transform is applied after the child's)
        /// </summary>
        public SvgMatrix Multiply(SvgMatrix inner)
        {
            return new SvgMatrix(
                (A * inner.A) + (C * inner.B),
                (B * inner.A) + (D * inner.B),
                (A * inner.C) + (C * inner.D),
                (B * inner.C) + (D * inner.D),
                (A * inner.E) + (C * inner.F) + E,
                (B * inner.E) + (D * inner.F) + F);
        }

        public (double X, double Y) Apply(double x, double y)
        {
            return ((A * x) + (C * y) + E, (B * x) + (D * y) + F);
        }

        public SvgMatrix Invert()
        {
            var det = (A * D) - (B * C);
            if (Math.Abs(det) < 1e-12) return Identity;

            return new SvgMatrix(
                D / det,
                -B / det,
                -C / det,
                A / det,
                ((C * F) - (D * E)) / det,
                ((B * E) - (A * F)) / det);
        }

        /// <summary>
        /// The PDF operator that applies this matrix, eg: "1 0 0 1 10 20 cm"
        /// </summary>
        public string ToPdf()
        {
            return ToNumbers() + " cm";
        }

        /// <summary>
        /// The six numbers of the matrix, eg: "1 0 0 1 10 20"
        /// </summary>
        public string ToNumbers()
        {
            return string.Join(" ", SvgNumber.Format(A), SvgNumber.Format(B), SvgNumber.Format(C),
                SvgNumber.Format(D), SvgNumber.Format(E), SvgNumber.Format(F));
        }

        /// <summary>
        /// Parses an SVG transform list, eg: "translate(10 20) rotate(45, 5, 5) scale(2)"
        /// </summary>
        public static SvgMatrix Parse(string value)
        {
            var result = Identity;
            if (string.IsNullOrWhiteSpace(value)) return result;

            var pos = 0;
            while (pos < value.Length)
            {
                while (pos < value.Length && (char.IsWhiteSpace(value[pos]) || value[pos] == ',')) pos++;
                if (pos >= value.Length) break;

                var nameStart = pos;
                while (pos < value.Length && char.IsLetter(value[pos])) pos++;
                var name = value.Substring(nameStart, pos - nameStart);

                var open = value.IndexOf('(', pos);
                var close = value.IndexOf(')', pos);
                if (name.Length == 0 || open < 0 || close < open)
                {
                    throw new FormatException($"The SVG transform '{value}' is not valid.");
                }

                var args = SvgNumber.ParseList(value.Substring(open + 1, close - open - 1));
                pos = close + 1;

                double Arg(int i, double fallback = 0) => i < args.Count ? args[i] : fallback;

                SvgMatrix transform;
                switch (name)
                {
                    case "matrix":
                        if (args.Count != 6) throw new FormatException($"The SVG transform '{value}' is not valid.");
                        transform = new SvgMatrix(args[0], args[1], args[2], args[3], args[4], args[5]);
                        break;
                    case "translate":
                        transform = Translate(Arg(0), Arg(1));
                        break;
                    case "scale":
                        transform = Scale(Arg(0, 1), Arg(1, Arg(0, 1)));
                        break;
                    case "rotate":
                        transform = Translate(Arg(1), Arg(2)).Multiply(Rotate(Arg(0))).Multiply(Translate(-Arg(1), -Arg(2)));
                        break;
                    case "skewX":
                        transform = new SvgMatrix(1, 0, Math.Tan(Arg(0) * Math.PI / 180), 1, 0, 0);
                        break;
                    case "skewY":
                        transform = new SvgMatrix(1, Math.Tan(Arg(0) * Math.PI / 180), 0, 1, 0, 0);
                        break;
                    default:
                        throw new FormatException($"The SVG transform '{name}' is not recognised.");
                }

                // each transform in the list applies inside the ones before it
                result = result.Multiply(transform);
            }

            return result;
        }
    }

    /// <summary>
    /// Reading and writing numbers in SVG and PDF syntax
    /// </summary>
    internal static class SvgNumber
    {
        /// <summary>
        /// Writes a number for PDF: invariant culture, at most 4 decimal places, no exponent
        /// </summary>
        public static string Format(double value)
        {
            var rounded = Math.Round(value, 4);
            if (rounded == 0) return "0"; // avoids "-0"
            return rounded.ToString("0.####", CultureInfo.InvariantCulture);
        }

        /// <summary>
        /// Reads a number following the SVG grammar from position <paramref name="pos"/>,
        /// eg: "-1.5e-3", ".5", "10." Returns false if there is no number there.
        /// </summary>
        public static bool TryRead(string text, ref int pos, out double value)
        {
            value = 0;
            var start = pos;
            var i = pos;

            if (i < text.Length && (text[i] == '+' || text[i] == '-')) i++;

            var digits = 0;
            while (i < text.Length && char.IsDigit(text[i])) { i++; digits++; }
            if (i < text.Length && text[i] == '.')
            {
                i++;
                while (i < text.Length && char.IsDigit(text[i])) { i++; digits++; }
            }
            if (digits == 0) return false;

            // an exponent, only if it is followed by digits (so "2em" isn't read as an exponent)
            if (i < text.Length && (text[i] == 'e' || text[i] == 'E'))
            {
                var j = i + 1;
                if (j < text.Length && (text[j] == '+' || text[j] == '-')) j++;
                if (j < text.Length && char.IsDigit(text[j]))
                {
                    while (j < text.Length && char.IsDigit(text[j])) j++;
                    i = j;
                }
            }

            value = double.Parse(text.Substring(start, i - start), NumberStyles.Float, CultureInfo.InvariantCulture);
            pos = i;
            return true;
        }

        /// <summary>
        /// Skips whitespace and at most one comma
        /// </summary>
        public static void SkipSeparators(string text, ref int pos)
        {
            while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
            if (pos < text.Length && text[pos] == ',') pos++;
            while (pos < text.Length && char.IsWhiteSpace(text[pos])) pos++;
        }

        /// <summary>
        /// Reads a list of numbers separated by whitespace and/or commas
        /// </summary>
        public static List<double> ParseList(string text)
        {
            var result = new List<double>();
            if (text == null) return result;

            var pos = 0;
            SkipSeparators(text, ref pos);
            while (pos < text.Length && TryRead(text, ref pos, out var value))
            {
                result.Add(value);
                SkipSeparators(text, ref pos);
            }
            return result;
        }
    }
}
