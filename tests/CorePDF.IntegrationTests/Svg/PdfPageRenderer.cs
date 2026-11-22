using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace CorePDF.IntegrationTests.Svg
{
    /// <summary>
    /// Draws the vector graphics of a PDF page with SkiaSharp, so the output can be compared with
    /// a reference rendering. It understands the path, colour, graphics state, transparency,
    /// shading pattern, soft mask, text and form XObject operators CorePDF writes. Text is drawn
    /// from embedded (Type0) fonts using the font program in the document; text in the standard
    /// fonts and images are ignored.
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
            public string FillPattern;
            public string StrokePattern;
            public float FillAlpha = 1;
            public float StrokeAlpha = 1;
            public float LineWidth = 1;
            public SKStrokeCap Cap = SKStrokeCap.Butt;
            public SKStrokeJoin Join = SKStrokeJoin.Miter;
            public float MiterLimit = 10;
            public float[] Dash;
            public float DashPhase;
            public SKPath Clip;
            public Dictionary<string, object> SoftMask;
            public SKMatrix SoftMaskMatrix;
            public string FontName;
            public float FontSize;
            public int RenderingMode;

            public GraphicsState Clone()
            {
                var copy = (GraphicsState)MemberwiseClone();
                copy.Clip = Clip == null ? null : new SKPath(Clip);
                return copy;
            }
        }

        /// <summary>
        /// The content stream being drawn: its resources, and the co-ordinate system it started
        /// in (which patterns are positioned relative to)
        /// </summary>
        private sealed class Context
        {
            public Dictionary<string, object> Resources;
            public SKMatrix FormBase;
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
            var dictionary = (Dictionary<string, object>)PdfObjectParser.Parse(page.Dictionary);
            var data = new StringBuilder();
            foreach (PdfReference reference in (List<object>)dictionary["Contents"])
            {
                var stream = _pdf.Objects[reference.Number].Stream;
                if (stream != null) data.Append(Encoding.Latin1.GetString(stream)).Append('\n');
            }

            Render(data.ToString(), new Context { Resources = Dict(dictionary["Resources"]), FormBase = SKMatrix.Identity }, new GraphicsState());
        }

        #region Objects

        private object Resolve(object value)
        {
            return value is PdfReference reference ? PdfObjectParser.Parse(_pdf.Objects[reference.Number].Dictionary) : value;
        }

        private Dictionary<string, object> Dict(object value) => (Dictionary<string, object>)Resolve(value);

        private static float F(object value) => Convert.ToSingle(value, CultureInfo.InvariantCulture);

        private static float[] Floats(object value) => ((List<object>)value).Select(F).ToArray();

        private object Resource(Context context, string category, string name)
        {
            return Dict(context.Resources[category])[name];
        }

        #endregion

        private void Render(string content, Context context, GraphicsState initial)
        {
            var state = initial;
            var stack = new Stack<GraphicsState>();
            var operands = new List<object>();
            var path = new SKPath();
            var clipPending = (SKPathFillType?)null;
            var textMatrix = SKMatrix.Identity;

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
                    case "rg": state.Fill = new SKColor(B(N(0)), B(N(1)), B(N(2))); state.FillPattern = null; break;
                    case "RG": state.Stroke = new SKColor(B(N(0)), B(N(1)), B(N(2))); state.StrokePattern = null; break;
                    case "g": state.Fill = new SKColor(B(N(0)), B(N(0)), B(N(0))); state.FillPattern = null; break;
                    case "G": state.Stroke = new SKColor(B(N(0)), B(N(0)), B(N(0))); state.StrokePattern = null; break;
                    case "cs": case "CS": break; // the colour space is implied by the colour operator that follows
                    case "scn": if (operands.Count == 1 && operands[0] is string fillPattern) state.FillPattern = fillPattern; break;
                    case "SCN": if (operands.Count == 1 && operands[0] is string strokePattern) state.StrokePattern = strokePattern; break;
                    case "gs": ApplyExtGState(context, (string)operands[0], state); break;
                    case "m": path.MoveTo(N(0), N(1)); break;
                    case "l": path.LineTo(N(0), N(1)); break;
                    case "c": path.CubicTo(N(0), N(1), N(2), N(3), N(4), N(5)); break;
                    case "v": path.CubicTo(path.LastPoint, new SKPoint(N(0), N(1)), new SKPoint(N(2), N(3))); break;
                    case "y": path.CubicTo(N(0), N(1), N(2), N(3), N(2), N(3)); break;
                    case "h": path.Close(); break;
                    case "re": path.AddRect(SKRect.Create(N(0), N(1), N(2), N(3))); break;
                    case "W": clipPending = SKPathFillType.Winding; break;
                    case "W*": clipPending = SKPathFillType.EvenOdd; break;
                    case "f": case "F": Paint(path, state, context, SKPathFillType.Winding, fill: true, stroke: false); break;
                    case "f*": Paint(path, state, context, SKPathFillType.EvenOdd, fill: true, stroke: false); break;
                    case "S": Paint(path, state, context, SKPathFillType.Winding, fill: false, stroke: true); break;
                    case "s": path.Close(); Paint(path, state, context, SKPathFillType.Winding, fill: false, stroke: true); break;
                    case "B": Paint(path, state, context, SKPathFillType.Winding, fill: true, stroke: true); break;
                    case "B*": Paint(path, state, context, SKPathFillType.EvenOdd, fill: true, stroke: true); break;
                    case "b": path.Close(); Paint(path, state, context, SKPathFillType.Winding, fill: true, stroke: true); break;
                    case "b*": path.Close(); Paint(path, state, context, SKPathFillType.EvenOdd, fill: true, stroke: true); break;
                    case "n": break;
                    case "sh": DrawShading(context, (string)operands[0], state); break;
                    case "BT": textMatrix = SKMatrix.Identity; break;
                    case "Tf": state.FontName = (string)operands[0]; state.FontSize = N(1); break;
                    case "Tr": state.RenderingMode = (int)N(0); break;
                    case "Tm": textMatrix = new SKMatrix(N(0), N(2), N(4), N(1), N(3), N(5), 0, 0, 1); break;
                    case "Td": textMatrix = SKMatrix.Concat(textMatrix, SKMatrix.CreateTranslation(N(0), N(1))); break;
                    case "Tj": DrawText(context, state, new List<object> { operands[0] }, ref textMatrix); break;
                    case "TJ": DrawText(context, state, (List<object>)operands[0], ref textMatrix); break;
                    case "Do": DrawXObject(context, (string)operands[0], state); break;
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

        private void Paint(SKPath path, GraphicsState state, Context context, SKPathFillType fillType, bool fill, bool stroke)
        {
            path.FillType = fillType;

            WithSoftMask(state, () =>
            {
                _canvas.Save();
                if (state.Clip != null)
                {
                    _canvas.ClipPath(state.Clip, antialias: true);
                }
                _canvas.SetMatrix(SKMatrix.Concat(_device, state.Ctm));

                if (fill)
                {
                    using var paint = new SKPaint { IsAntialias = true, Style = SKPaintStyle.Fill, Color = state.Fill.WithAlpha(B(state.FillAlpha)) };
                    if (state.FillPattern != null) paint.Shader = PatternShader(context, state.FillPattern, state.Ctm, state.FillAlpha);
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
                    if (state.StrokePattern != null) paint.Shader = PatternShader(context, state.StrokePattern, state.Ctm, state.StrokeAlpha);
                    if (state.Dash != null)
                    {
                        paint.PathEffect = SKPathEffect.CreateDash(state.Dash.Length % 2 == 0 ? state.Dash : Repeat(state.Dash), state.DashPhase);
                    }
                    _canvas.DrawPath(path, paint);
                }
                _canvas.Restore();
            });
        }

        /// <summary>
        /// Draws with the current soft mask (if any): the drawing is made on its own layer, which
        /// the mask's luminosity is then applied to as opacity
        /// </summary>
        private void WithSoftMask(GraphicsState state, Action draw)
        {
            if (state.SoftMask == null)
            {
                draw();
                return;
            }

            _canvas.SaveLayer();
            draw();

            using var maskPaint = new SKPaint { BlendMode = SKBlendMode.DstIn, ColorFilter = SKColorFilter.CreateLumaColor() };
            _canvas.SaveLayer(maskPaint);
            var group = (PdfReference)state.SoftMask["G"];
            var form = _pdf.Objects[group.Number];
            var formDictionary = (Dictionary<string, object>)PdfObjectParser.Parse(form.Dictionary);
            Render(Encoding.Latin1.GetString(form.Stream), new Context { Resources = Dict(formDictionary["Resources"]), FormBase = state.SoftMaskMatrix },
                new GraphicsState { Ctm = state.SoftMaskMatrix, Clip = state.Clip });
            _canvas.Restore();

            _canvas.Restore();
        }

        private static float[] Repeat(float[] values)
        {
            var result = new float[values.Length * 2];
            values.CopyTo(result, 0);
            values.CopyTo(result, values.Length);
            return result;
        }

        private void ApplyExtGState(Context context, string name, GraphicsState state)
        {
            var dictionary = Dict(Resource(context, "ExtGState", name));
            if (dictionary.TryGetValue("ca", out var ca)) state.FillAlpha = F(ca);
            if (dictionary.TryGetValue("CA", out var CA)) state.StrokeAlpha = F(CA);
            if (dictionary.TryGetValue("SMask", out var mask))
            {
                state.SoftMask = mask is PdfName ? null : Dict(mask);
                state.SoftMaskMatrix = state.Ctm;
            }
        }

        #region Shadings

        /// <summary>
        /// A shader for a shading pattern. The pattern is positioned relative to the co-ordinate
        /// system of the form it is used in, while the shader works relative to the current
        /// transformation matrix (which the canvas is set to).
        /// </summary>
        private SKShader PatternShader(Context context, string name, SKMatrix current, float alpha)
        {
            var pattern = Dict(Resource(context, "Pattern", name));
            var matrix = pattern.TryGetValue("Matrix", out var m) ? Floats(m) : new float[] { 1, 0, 0, 1, 0, 0 };
            var patternMatrix = new SKMatrix(matrix[0], matrix[2], matrix[4], matrix[1], matrix[3], matrix[5], 0, 0, 1);

            current.TryInvert(out var inverse);
            var local = SKMatrix.Concat(inverse, SKMatrix.Concat(context.FormBase, patternMatrix));
            return Shader(Dict(pattern["Shading"]), local, alpha);
        }

        private void DrawShading(Context context, string name, GraphicsState state)
        {
            using var paint = new SKPaint { IsAntialias = true, Shader = Shader(Dict(Resource(context, "Shading", name)), SKMatrix.Identity, state.FillAlpha) };
            _canvas.Save();
            if (state.Clip != null) _canvas.ClipPath(state.Clip, antialias: true);
            _canvas.SetMatrix(SKMatrix.Concat(_device, state.Ctm));
            _canvas.DrawPaint(paint);
            _canvas.Restore();
        }

        private SKShader Shader(Dictionary<string, object> shading, SKMatrix local, float alpha)
        {
            var type = (int)F(shading["ShadingType"]);
            var coords = Floats(shading["Coords"]);
            var domain = shading.TryGetValue("Domain", out var d) ? Floats(d) : new float[] { 0, 1 };
            var gray = ((PdfName)shading["ColorSpace"]).Value == "DeviceGray";
            var function = Dict(shading["Function"]);

            // sample the function finely enough to follow every piece of a stitched function
            var samples = Math.Clamp(Pieces(function) * 32, 64, 8192);
            var colours = new SKColor[samples];
            var positions = new float[samples];
            for (var i = 0; i < samples; i++)
            {
                positions[i] = i / (float)(samples - 1);
                var values = Evaluate(function, domain[0] + (positions[i] * (domain[1] - domain[0])));
                colours[i] = gray
                    ? new SKColor(B(values[0]), B(values[0]), B(values[0]), B(alpha))
                    : new SKColor(B(values[0]), B(values[1]), B(values[2]), B(alpha));
            }

            return type == 2
                ? SKShader.CreateLinearGradient(new SKPoint(coords[0], coords[1]), new SKPoint(coords[2], coords[3]), colours, positions, SKShaderTileMode.Clamp, local)
                : SKShader.CreateTwoPointConicalGradient(new SKPoint(coords[0], coords[1]), coords[2], new SKPoint(coords[3], coords[4]), coords[5], colours, positions, SKShaderTileMode.Clamp, local);
        }

        private int Pieces(Dictionary<string, object> function)
        {
            return (int)F(function["FunctionType"]) == 3
                ? ((List<object>)function["Functions"]).Sum(f => Pieces(Dict(f)))
                : 1;
        }

        /// <summary>
        /// Evaluates an exponential (type 2) or stitching (type 3) function
        /// </summary>
        private float[] Evaluate(Dictionary<string, object> function, float t)
        {
            var domain = Floats(function["Domain"]);
            t = Math.Clamp(t, domain[0], domain[1]);

            if ((int)F(function["FunctionType"]) == 2)
            {
                var c0 = function.TryGetValue("C0", out var a) ? Floats(a) : new float[] { 0 };
                var c1 = function.TryGetValue("C1", out var b) ? Floats(b) : new float[] { 1 };
                var n = F(function["N"]);
                var x = (float)Math.Pow((t - domain[0]) / Math.Max(1e-9f, domain[1] - domain[0]), n);
                return c0.Select((v, i) => v + (x * (c1[i] - v))).ToArray();
            }

            var functions = (List<object>)function["Functions"];
            var bounds = Floats(function["Bounds"]);
            var encode = Floats(function["Encode"]);

            var k = 0;
            while (k < bounds.Length && t >= bounds[k]) k++;
            var low = k == 0 ? domain[0] : bounds[k - 1];
            var high = k == bounds.Length ? domain[1] : bounds[k];
            var e0 = encode[2 * k];
            var e1 = encode[(2 * k) + 1];
            var mapped = high > low ? e0 + ((t - low) * (e1 - e0) / (high - low)) : e0;
            return Evaluate(Dict(functions[k]), mapped);
        }

        #endregion

        #region Text

        private sealed class LoadedFont
        {
            public SKTypeface Typeface;
            public Dictionary<int, float> Widths = new Dictionary<int, float>();
            public float DefaultWidth = 1000;
        }

        private readonly Dictionary<int, LoadedFont> _fonts = new Dictionary<int, LoadedFont>();

        /// <summary>
        /// Loads an embedded composite font: its glyph widths and its (TrueType) font program
        /// </summary>
        private LoadedFont LoadFont(Context context, string name)
        {
            var reference = (PdfReference)Resource(context, "Font", name);
            if (_fonts.TryGetValue(reference.Number, out var loaded)) return loaded;

            var font = Dict(reference);
            if (((PdfName)font["Subtype"]).Value != "Type0")
            {
                _fonts[reference.Number] = null;
                return null;
            }

            var cidFont = Dict(((List<object>)font["DescendantFonts"])[0]);
            var descriptor = Dict(cidFont["FontDescriptor"]);
            var program = _pdf.Objects[((PdfReference)descriptor["FontFile2"]).Number].Stream;

            loaded = new LoadedFont { Typeface = SKTypeface.FromData(SKData.CreateCopy(program)) };
            if (cidFont.TryGetValue("DW", out var dw)) loaded.DefaultWidth = F(dw);
            if (cidFont.TryGetValue("W", out var w))
            {
                var widths = (List<object>)w;
                for (var i = 0; i < widths.Count;)
                {
                    var first = (int)F(widths[i]);
                    if (widths[i + 1] is List<object> list)
                    {
                        for (var j = 0; j < list.Count; j++) loaded.Widths[first + j] = F(list[j]);
                        i += 2;
                    }
                    else
                    {
                        for (var g = first; g <= (int)F(widths[i + 1]); g++) loaded.Widths[g] = F(widths[i + 2]);
                        i += 3;
                    }
                }
            }

            _fonts[reference.Number] = loaded;
            return loaded;
        }

        /// <summary>
        /// Draws text shown with Tj or TJ. Glyphs are drawn as their outlines, filled and/or
        /// stroked according to the text rendering mode.
        /// </summary>
        private void DrawText(Context context, GraphicsState state, List<object> items, ref SKMatrix textMatrix)
        {
            var font = LoadFont(context, state.FontName);
            var size = state.FontSize;
            var x = 0f;
            var outline = new SKPath();

            using var skFont = font == null ? null : new SKFont(font.Typeface, size);
            foreach (var item in items)
            {
                if (item is double adjustment)
                {
                    x -= (float)adjustment / 1000 * size;
                }
                else if (item is byte[] bytes && font != null)
                {
                    for (var i = 0; i + 1 < bytes.Length; i += 2)
                    {
                        var gid = (ushort)((bytes[i] << 8) | bytes[i + 1]);
                        using var glyph = skFont.GetGlyphPath(gid);
                        if (glyph != null)
                        {
                            // glyph outlines have y going down; text space has y going up
                            glyph.Transform(SKMatrix.Concat(SKMatrix.CreateTranslation(x, 0), SKMatrix.CreateScale(1, -1)));
                            outline.AddPath(glyph);
                        }
                        x += (font.Widths.TryGetValue(gid, out var width) ? width : font.DefaultWidth) / 1000 * size;
                    }
                }
            }

            var mode = state.RenderingMode;
            if (font != null && mode != 3 && mode != 7)
            {
                var current = SKMatrix.Concat(state.Ctm, textMatrix);
                var textState = state.Clone();
                textState.Ctm = current;
                var fill = mode == 0 || mode == 2 || mode == 4 || mode == 6;
                var stroke = mode == 1 || mode == 2 || mode == 5 || mode == 6;
                Paint(outline, textState, context, SKPathFillType.Winding, fill, stroke);
            }

            textMatrix = SKMatrix.Concat(textMatrix, SKMatrix.CreateTranslation(x, 0));
        }

        #endregion

        private void DrawXObject(Context context, string name, GraphicsState state)
        {
            var reference = (PdfReference)Resource(context, "XObject", name);
            var xobject = _pdf.Objects[reference.Number];
            var dictionary = (Dictionary<string, object>)PdfObjectParser.Parse(xobject.Dictionary);
            if (((PdfName)dictionary["Subtype"]).Value != "Form") return; // images aren't compared

            var inner = state.Clone();
            if (dictionary.TryGetValue("Matrix", out var m))
            {
                var matrix = Floats(m);
                inner.Ctm = SKMatrix.Concat(inner.Ctm, new SKMatrix(matrix[0], matrix[2], matrix[4], matrix[1], matrix[3], matrix[5], 0, 0, 1));
            }

            var content = Encoding.Latin1.GetString(xobject.Stream ?? new byte[0]);
            var formContext = new Context { Resources = Dict(dictionary["Resources"]), FormBase = inner.Ctm };

            // a transparency group is drawn on its own and then blended using the current opacity
            if (dictionary.ContainsKey("Group") && (state.FillAlpha < 1))
            {
                using var layer = new SKPaint { Color = SKColors.Black.WithAlpha(B(state.FillAlpha)) };
                _canvas.SaveLayer(layer);
                inner.FillAlpha = 1;
                inner.StrokeAlpha = 1;
                Render(content, formContext, inner);
                _canvas.Restore();
            }
            else
            {
                Render(content, formContext, inner);
            }
        }

        private static byte B(float value) => (byte)Math.Round(Math.Clamp(value, 0, 1) * 255);

        #region Tokeniser

        private sealed class Operator
        {
            public string Name;
        }

        /// <summary>
        /// A literal string (only used for text in the standard fonts, which isn't drawn)
        /// </summary>
        private sealed class LiteralString
        {
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
                    token = new LiteralString();
                }
                else if (c == '<' && i + 1 < content.Length && content[i + 1] != '<')
                {
                    var start = ++i;
                    while (content[i] != '>') i++;
                    var hex = content.Substring(start, i - start);
                    i++;
                    token = Convert.FromHexString(hex.Length % 2 == 0 ? hex : hex + "0");
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
