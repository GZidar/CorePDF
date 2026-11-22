using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace CorePDF.IntegrationTests.Svg
{
    /// <summary>
    /// Draws the vector graphics of a PDF page with SkiaSharp, so the output can be compared with
    /// a reference rendering. It understands the path, colour, graphics state, transparency and
    /// form XObject operators CorePDF writes; text and images are ignored.
    /// </summary>
    internal sealed class PdfPageRenderer
    {
        private readonly PdfInspector _pdf;
        private readonly SKCanvas _canvas;
        private readonly SKMatrix _device;

        private sealed class GraphicsState
        {
            public SKMatrix Ctm = SKMatrix.Identity;
            public SKColor Fill = SKColors.Black;
            public SKColor Stroke = SKColors.Black;
            public float FillAlpha = 1;
            public float StrokeAlpha = 1;
            public float LineWidth = 1;
            public SKStrokeCap Cap = SKStrokeCap.Butt;
            public SKStrokeJoin Join = SKStrokeJoin.Miter;
            public float MiterLimit = 10;
            public float[] Dash;
            public float DashPhase;
            public SKPath Clip;

            public GraphicsState Clone()
            {
                var copy = (GraphicsState)MemberwiseClone();
                copy.Clip = Clip == null ? null : new SKPath(Clip);
                return copy;
            }
        }

        /// <param name="scaleX">horizontal pixels per PDF unit</param>
        /// <param name="scaleY">vertical pixels per PDF unit</param>
        /// <param name="pageHeight">the height of the area being drawn, in PDF units</param>
        public PdfPageRenderer(PdfInspector pdf, SKCanvas canvas, float scaleX, float scaleY, float pageHeight)
        {
            _pdf = pdf;
            _canvas = canvas;

            // PDF's origin is the bottom left corner, with y going up
            _device = new SKMatrix(scaleX, 0, 0, 0, -scaleY, pageHeight * scaleY, 0, 0, 1);
        }

        public void RenderPage(PdfObjectInfo page)
        {
            var contents = Regex.Match(page.Dictionary, @"/Contents \[([^\]]*)\]").Groups[1].Value;
            var data = new StringBuilder();
            foreach (Match reference in Regex.Matches(contents, @"\d+ 0 R"))
            {
                var stream = _pdf.Resolve(reference.Value).Stream;
                if (stream != null) data.Append(Encoding.Latin1.GetString(stream)).Append('\n');
            }

            Render(data.ToString(), page.Dictionary, new GraphicsState());
        }

        private void Render(string content, string resources, GraphicsState initial)
        {
            var state = initial;
            var stack = new Stack<GraphicsState>();
            var operands = new List<object>();
            var path = new SKPath();
            var clipPending = (SKPathFillType?)null;

            foreach (var token in Tokenise(content))
            {
                if (!(token is Operator op))
                {
                    operands.Add(token);
                    continue;
                }

                float N(int i) => Convert.ToSingle(operands[i], CultureInfo.InvariantCulture);

                switch (op.Name)
                {
                    case "q": stack.Push(state.Clone()); break;
                    case "Q": state = stack.Pop(); break;
                    case "cm":
                        state.Ctm = SKMatrix.Concat(state.Ctm, new SKMatrix(N(0), N(2), N(4), N(1), N(3), N(5), 0, 0, 1));
                        break;
                    case "w": state.LineWidth = N(0); break;
                    case "J": state.Cap = (SKStrokeCap)(int)N(0); break;
                    case "j": state.Join = (int)N(0) switch { 1 => SKStrokeJoin.Round, 2 => SKStrokeJoin.Bevel, _ => SKStrokeJoin.Miter }; break;
                    case "M": state.MiterLimit = N(0); break;
                    case "d":
                        var dash = (List<object>)operands[0];
                        state.Dash = dash.Count == 0 ? null : dash.ConvertAll(d => Convert.ToSingle(d, CultureInfo.InvariantCulture)).ToArray();
                        state.DashPhase = N(1);
                        break;
                    case "rg": state.Fill = new SKColor(B(N(0)), B(N(1)), B(N(2))); break;
                    case "RG": state.Stroke = new SKColor(B(N(0)), B(N(1)), B(N(2))); break;
                    case "g": state.Fill = new SKColor(B(N(0)), B(N(0)), B(N(0))); break;
                    case "G": state.Stroke = new SKColor(B(N(0)), B(N(0)), B(N(0))); break;
                    case "gs": ApplyExtGState(resources, (string)operands[0], state); break;
                    case "m": path.MoveTo(N(0), N(1)); break;
                    case "l": path.LineTo(N(0), N(1)); break;
                    case "c": path.CubicTo(N(0), N(1), N(2), N(3), N(4), N(5)); break;
                    case "v": path.CubicTo(path.LastPoint, new SKPoint(N(0), N(1)), new SKPoint(N(2), N(3))); break;
                    case "y": path.CubicTo(N(0), N(1), N(2), N(3), N(2), N(3)); break;
                    case "h": path.Close(); break;
                    case "re": path.AddRect(SKRect.Create(N(0), N(1), N(2), N(3))); break;
                    case "W": clipPending = SKPathFillType.Winding; break;
                    case "W*": clipPending = SKPathFillType.EvenOdd; break;
                    case "f": case "F": Paint(path, state, SKPathFillType.Winding, fill: true, stroke: false); break;
                    case "f*": Paint(path, state, SKPathFillType.EvenOdd, fill: true, stroke: false); break;
                    case "S": Paint(path, state, SKPathFillType.Winding, fill: false, stroke: true); break;
                    case "s": path.Close(); Paint(path, state, SKPathFillType.Winding, fill: false, stroke: true); break;
                    case "B": Paint(path, state, SKPathFillType.Winding, fill: true, stroke: true); break;
                    case "B*": Paint(path, state, SKPathFillType.EvenOdd, fill: true, stroke: true); break;
                    case "b": path.Close(); Paint(path, state, SKPathFillType.Winding, fill: true, stroke: true); break;
                    case "b*": path.Close(); Paint(path, state, SKPathFillType.EvenOdd, fill: true, stroke: true); break;
                    case "n": break;
                    case "Do": DrawXObject(resources, (string)operands[0], state); break;
                }

                if (op.Name is "f" or "F" or "f*" or "S" or "s" or "B" or "B*" or "b" or "b*" or "n")
                {
                    if (clipPending != null)
                    {
                        // clipping paths are in user space, so store them in device space
                        var clip = new SKPath(path) { FillType = clipPending.Value };
                        clip.Transform(SKMatrix.Concat(_device, state.Ctm));
                        state.Clip = state.Clip == null ? clip : state.Clip.Op(clip, SKPathOp.Intersect);
                        clipPending = null;
                    }
                    path = new SKPath();
                }

                operands.Clear();
            }
        }

        private void Paint(SKPath path, GraphicsState state, SKPathFillType fillType, bool fill, bool stroke)
        {
            path.FillType = fillType;
            _canvas.Save();
            if (state.Clip != null)
            {
                _canvas.ClipPath(state.Clip, antialias: true);
            }
            _canvas.SetMatrix(SKMatrix.Concat(_device, state.Ctm));

            if (fill)
            {
                using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = state.Fill.WithAlpha(B(state.FillAlpha)) };
                _canvas.DrawPath(path, paint);
            }
            if (stroke)
            {
                using var paint = new SKPaint
                {
                    IsAntialias = true,
                    Style = SKPaintStyle.Stroke,
                    Color = state.Stroke.WithAlpha(B(state.StrokeAlpha)),
                    StrokeWidth = state.LineWidth,
                    StrokeCap = state.Cap,
                    StrokeJoin = state.Join,
                    StrokeMiter = state.MiterLimit
                };
                if (state.Dash != null)
                {
                    paint.PathEffect = SKPathEffect.CreateDash(state.Dash.Length % 2 == 0 ? state.Dash : Repeat(state.Dash), state.DashPhase);
                }
                _canvas.DrawPath(path, paint);
            }
            _canvas.Restore();
        }

        private static float[] Repeat(float[] values)
        {
            var result = new float[values.Length * 2];
            values.CopyTo(result, 0);
            values.CopyTo(result, values.Length);
            return result;
        }

        private void ApplyExtGState(string resources, string name, GraphicsState state)
        {
            var dict = ResourceEntry(resources, "ExtGState", name);
            var ca = Regex.Match(dict, @"/ca ([\d.]+)");
            var CA = Regex.Match(dict, @"/CA ([\d.]+)");
            if (ca.Success) state.FillAlpha = float.Parse(ca.Groups[1].Value, CultureInfo.InvariantCulture);
            if (CA.Success) state.StrokeAlpha = float.Parse(CA.Groups[1].Value, CultureInfo.InvariantCulture);
        }

        private void DrawXObject(string resources, string name, GraphicsState state)
        {
            var entry = ResourceEntry(resources, "XObject", name);
            var xobject = _pdf.Resolve(entry);
            if (!xobject.Dictionary.Contains("/Subtype /Form")) return; // images aren't compared

            var inner = state.Clone();
            var matrix = Regex.Match(xobject.Dictionary, @"/Matrix \[([^\]]*)\]");
            if (matrix.Success)
            {
                var m = Array.ConvertAll(matrix.Groups[1].Value.Split(' ', StringSplitOptions.RemoveEmptyEntries), v => float.Parse(v, CultureInfo.InvariantCulture));
                inner.Ctm = SKMatrix.Concat(inner.Ctm, new SKMatrix(m[0], m[2], m[4], m[1], m[3], m[5], 0, 0, 1));
            }

            var content = Encoding.Latin1.GetString(xobject.Stream ?? new byte[0]);

            // a transparency group is drawn on its own and then blended using the current opacity
            if (xobject.Dictionary.Contains("/Group") && (state.FillAlpha < 1))
            {
                using var layer = new SKPaint { Color = SKColors.Black.WithAlpha(B(state.FillAlpha)) };
                _canvas.SaveLayer(layer);
                inner.FillAlpha = 1;
                inner.StrokeAlpha = 1;
                Render(content, xobject.Dictionary, inner);
                _canvas.Restore();
            }
            else
            {
                Render(content, xobject.Dictionary, inner);
            }
        }

        /// <summary>
        /// Finds a named entry in a resource category, eg: /XObject &lt;&lt; /I1 5 0 R &gt;&gt;
        /// </summary>
        private string ResourceEntry(string resources, string category, string name)
        {
            var section = Regex.Match(resources, "/" + category + @"\s*<<((?:[^<>]|<<(?:[^<>]|<<[^<>]*>>)*>>)*)>>");
            if (!section.Success)
            {
                // the resources dictionary may itself be an indirect object
                var reference = Regex.Match(resources, @"/Resources (\d+ 0 R)");
                if (reference.Success) return ResourceEntry(_pdf.Resolve(reference.Groups[1].Value).Dictionary, category, name);
                throw new InvalidOperationException($"No {category} resources for {name}");
            }

            var entry = Regex.Match(section.Groups[1].Value, "/" + Regex.Escape(name) + @"\s*(<<(?:[^<>]|<<[^<>]*>>)*>>|\d+ 0 R)");
            if (!entry.Success) throw new InvalidOperationException($"Resource {name} not found");
            return entry.Groups[1].Value;
        }

        private static byte B(float value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);

        #region Tokeniser

        private sealed class Operator
        {
            public string Name;
        }

        private static IEnumerable<object> Tokenise(string content)
        {
            var stack = new Stack<List<object>>();
            var i = 0;
            while (i < content.Length)
            {
                var c = content[i];
                if (char.IsWhiteSpace(c)) { i++; continue; }

                object token;
                if (c == '%')
                {
                    while (i < content.Length && content[i] != '\n') i++;
                    continue;
                }
                if (c == '[')
                {
                    stack.Push(new List<object>());
                    i++;
                    continue;
                }
                if (c == ']')
                {
                    token = stack.Pop();
                    i++;
                }
                else if (c == '(')
                {
                    // strings only appear in text, which isn't drawn
                    var depth = 0;
                    do
                    {
                        if (content[i] == '\\') i++;
                        else if (content[i] == '(') depth++;
                        else if (content[i] == ')') depth--;
                        i++;
                    } while (depth > 0);
                    token = "";
                }
                else if (c == '<' && i + 1 < content.Length && content[i + 1] != '<')
                {
                    while (content[i] != '>') i++;
                    i++;
                    token = "";
                }
                else if (c == '/')
                {
                    var start = ++i;
                    while (i < content.Length && !char.IsWhiteSpace(content[i]) && "/[]()<>".IndexOf(content[i]) < 0) i++;
                    token = content.Substring(start, i - start);
                }
                else if (char.IsDigit(c) || c == '-' || c == '+' || c == '.')
                {
                    var start = i++;
                    while (i < content.Length && (char.IsDigit(content[i]) || content[i] == '.')) i++;
                    token = double.Parse(content.Substring(start, i - start), CultureInfo.InvariantCulture);
                }
                else
                {
                    var start = i;
                    while (i < content.Length && !char.IsWhiteSpace(content[i]) && "/[]()<>".IndexOf(content[i]) < 0) i++;
                    token = new Operator { Name = content.Substring(start, i - start) };
                }

                if (stack.Count > 0 && !(token is Operator)) stack.Peek().Add(token);
                else yield return token;
            }
        }

        #endregion
    }
}
