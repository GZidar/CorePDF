using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace CorePDF.Svg
{
    /// <summary>
    /// A form XObject (a reusable drawing) used by an SVG image: a transparency group (for
    /// opacity applied to several shapes) or a soft mask (for gradients with varying opacity)
    /// </summary>
    internal sealed class SvgForm
    {
        public string Content { get; set; }
        public double[] BBox { get; set; }
        public string Group { get; set; }
    }

    /// <summary>
    /// An SVG image converted to PDF drawing operations, ready to be written as a form XObject
    /// (a reusable drawing) with a bounding box of [0 0 Width Height].
    /// </summary>
    internal sealed class SvgDrawing
    {
        /// <summary>
        /// The size of the image in CSS pixels (which CorePDF treats as points)
        /// </summary>
        public double Width { get; set; }
        public double Height { get; set; }

        /// <summary>
        /// The content stream of the drawing
        /// </summary>
        public string Content { get; set; }

        /// <summary>
        /// Other forms the drawing uses, named Fm1, Fm2, etc. Each is written as its own object.
        /// </summary>
        public List<SvgForm> Forms { get; } = new List<SvgForm>();

        /// <summary>
        /// Graphics state dictionaries, named GS1, GS2, etc
        /// </summary>
        public List<string> GraphicsStates { get; } = new List<string>();

        /// <summary>
        /// Pattern dictionaries (for gradients), named P1, P2, etc
        /// </summary>
        public List<string> Patterns { get; } = new List<string>();

        /// <summary>
        /// Shading dictionaries drawn directly with the sh operator, named Sh1, Sh2, etc
        /// </summary>
        public List<string> Shadings { get; } = new List<string>();

        /// <summary>
        /// The resources dictionary shared by the drawing and its forms. Dictionaries refer to
        /// forms as {FmN}, which are replaced with references to the form objects.
        /// </summary>
        public string Resources(Func<int, int> formObjectNumber)
        {
            var result = new StringBuilder("<<");
            Section(result, "ExtGState", "GS", GraphicsStates);
            Section(result, "Pattern", "P", Patterns);
            Section(result, "Shading", "Sh", Shadings);

            if (Forms.Count > 0)
            {
                result.Append(" /XObject <<");
                for (var i = 0; i < Forms.Count; i++)
                {
                    result.AppendFormat(" /Fm{0} {{Fm{0}}}", i + 1);
                }
                result.Append(" >>");
            }
            result.Append(" >>");

            var resources = result.ToString();
            for (var i = Forms.Count; i >= 1; i--)
            {
                resources = resources.Replace("{Fm" + i + "}", formObjectNumber(i - 1) + " 0 R");
            }
            return resources;
        }

        private static void Section(StringBuilder result, string category, string prefix, List<string> entries)
        {
            if (entries.Count == 0) return;

            result.Append(" /").Append(category).Append(" <<");
            for (var i = 0; i < entries.Count; i++)
            {
                result.Append(" /").Append(prefix).Append(i + 1).Append(' ').Append(entries[i]);
            }
            result.Append(" >>");
        }
    }

    /// <summary>
    /// Converts SVG images into PDF drawing operations, so they stay sharp at any size.
    ///
    /// Supported: the basic shapes and paths, groups, use/symbol/defs, nested svg elements,
    /// transforms, viewBox and preserveAspectRatio, presentation attributes, style attributes and
    /// &lt;style&gt; sheets, colours, opacity, strokes (including dashes) and clipping paths.
    /// Elements that can't be drawn correctly yet (text, images, gradients, patterns, masks,
    /// filters and markers) cause a NotSupportedException rather than being drawn wrongly.
    /// </summary>
    internal sealed class SvgConverter
    {
        private const int MAXDEPTH = 64;

        private readonly Dictionary<string, XElement> _ids = new Dictionary<string, XElement>();
        private readonly SvgStyleSheet _styleSheet = new SvgStyleSheet();
        private readonly SvgDrawing _drawing = new SvgDrawing();
        private readonly Dictionary<string, int> _graphicsStates = new Dictionary<string, int>();
        private int _depth;

        /// <summary>
        /// The co-ordinate system of the form currently being written (relative to the image),
        /// which patterns are positioned relative to
        /// </summary>
        private SvgMatrix _formBase = SvgMatrix.Identity;

        /// <summary>
        /// Returns true if the data looks like an SVG document
        /// </summary>
        public static bool IsSvg(byte[] data)
        {
            var start = Encoding.UTF8.GetString(data, 0, Math.Min(data.Length, 4096));
            return start.Contains("<svg");
        }

        public static SvgDrawing Convert(byte[] data)
        {
            XDocument document;
            try
            {
                var settings = new XmlReaderSettings { DtdProcessing = DtdProcessing.Ignore, XmlResolver = null };
                using (var reader = XmlReader.Create(new MemoryStream(data), settings))
                {
                    document = XDocument.Load(reader);
                }
            }
            catch (XmlException ex)
            {
                throw new InvalidDataException("The SVG file is not valid XML: " + ex.Message, ex);
            }

            var root = document.Root;
            if (root == null || root.Name.LocalName != "svg")
            {
                throw new InvalidDataException("The file is not an SVG image.");
            }

            return new SvgConverter().ConvertRoot(root);
        }

        private SvgDrawing ConvertRoot(XElement root)
        {
            foreach (var element in root.DescendantsAndSelf())
            {
                var id = (string)element.Attribute("id");
                if (id != null && !_ids.ContainsKey(id)) _ids[id] = element;
                if (element.Name.LocalName == "style") _styleSheet.Add(element.Value);
            }

            // the size of the image
            var viewBox = ParseViewBox((string)root.Attribute("viewBox"));
            var width = Length((string)root.Attribute("width"), 0, allowPercent: false);
            var height = Length((string)root.Attribute("height"), 0, allowPercent: false);

            if (width == null && height == null)
            {
                width = viewBox?[2] ?? 300;
                height = viewBox?[3] ?? 150;
            }
            else if (width == null)
            {
                width = viewBox != null ? height * viewBox[2] / viewBox[3] : 300;
            }
            else if (height == null)
            {
                height = viewBox != null ? width * viewBox[3] / viewBox[2] : 150;
            }

            _drawing.Width = width.Value;
            _drawing.Height = height.Value;

            var output = new StringBuilder();

            // PDF's y axis goes up the page, SVG's goes down
            var flip = new SvgMatrix(1, 0, 0, -1, 0, height.Value);
            output.Append(flip.ToPdf()).Append('\n');

            var style = ComputeStyle(root, new SvgStyle());

            // a background on the root element is drawn by browsers, so draw it here too
            var background = Specified(root, "background-color") ?? Specified(root, "background");
            if (background != null && SvgColor.TryParse(background, out var backgroundColor) && backgroundColor.Alpha > 0)
            {
                output.Append(backgroundColor.ToPdf("rg")).Append('\n');
                output.AppendFormat("0 0 {0} {1} re f\n", SvgNumber.Format(width.Value), SvgNumber.Format(height.Value));
            }

            var transform = viewBox != null
                ? ViewBoxTransform(viewBox, width.Value, height.Value, (string)root.Attribute("preserveAspectRatio"))
                : SvgMatrix.Identity;
            if (!transform.IsIdentity) output.Append(transform.ToPdf()).Append('\n');

            var viewport = viewBox != null ? (viewBox[2], viewBox[3]) : (width.Value, height.Value);
            RenderChildren(root, style, output, flip.Multiply(transform), viewport);

            _drawing.Content = output.ToString();
            return _drawing;
        }

        #region Elements

        private void RenderChildren(XElement parent, SvgStyle style, StringBuilder output, SvgMatrix ctm, (double W, double H) viewport)
        {
            foreach (var child in parent.Elements())
            {
                Render(child, style, output, ctm, viewport);
            }
        }

        private void Render(XElement element, SvgStyle parentStyle, StringBuilder output, SvgMatrix ctm, (double W, double H) viewport, XElement useElement = null)
        {
            // only elements in the SVG namespace (or no namespace) are drawn; editor specific
            // elements, metadata, etc are ignored
            var ns = element.Name.NamespaceName;
            if (ns != "" && ns != "http://www.w3.org/2000/svg") return;

            var name = element.Name.LocalName;
            switch (name)
            {
                case "g":
                case "a":
                case "svg":
                case "use":
                case "switch":
                case "path":
                case "rect":
                case "circle":
                case "ellipse":
                case "line":
                case "polyline":
                case "polygon":
                    break;
                case "text":
                case "image":
                case "foreignObject":
                    throw new NotSupportedException($"SVG <{name}> elements are not supported yet. Convert text to paths (outlines) before using the image.");
                default:
                    // definitions, metadata, styles and anything unknown aren't drawn directly
                    return;
            }

            if (++_depth > MAXDEPTH)
            {
                throw new InvalidDataException("The SVG image is nested too deeply (or a <use> element refers to itself).");
            }

            try
            {
                var style = ComputeStyle(element, parentStyle);
                if (!style.Display) return;

                CheckSupported(style, name);

                var transform = SvgMatrix.Parse((string)element.Attribute("transform"));
                if (name == "use")
                {
                    transform = transform.Multiply(SvgMatrix.Translate(
                        Length((string)element.Attribute("x"), viewport.W) ?? 0,
                        Length((string)element.Attribute("y"), viewport.H) ?? 0));
                }
                var inner = ctm.Multiply(transform);

                var isContainer = name == "g" || name == "a" || name == "svg" || name == "use" || name == "switch";
                var paintsTwice = !isContainer && Paints(style.Fill, style) && Paints(style.Stroke, style);

                // opacity on a group (or a shape with both a fill and a stroke) applies to the
                // result as a whole, which needs a transparency group; otherwise it can simply
                // be combined with the fill and stroke opacity
                var group = style.Opacity < 1 && (isContainer || paintsTwice);
                if (style.Opacity <= 0) return;

                output.Append("q\n");
                if (!transform.IsIdentity) output.Append(transform.ToPdf()).Append('\n');

                if (style.ClipPath != "none" && !WriteClipPath(style.ClipPath, output, viewport))
                {
                    output.Append("Q\n");
                    return; // an empty clipping path hides the element
                }

                var target = output;
                var formBase = _formBase;
                if (group)
                {
                    // a group is drawn as its own form, which patterns are positioned relative to
                    target = new StringBuilder();
                    _formBase = inner;
                }

                var leafStyle = style;
                if (!group && style.Opacity < 1)
                {
                    leafStyle = style.Inherit();
                    CopyPaint(style, leafStyle);
                    leafStyle.FillOpacity *= style.Opacity;
                    leafStyle.StrokeOpacity *= style.Opacity;
                }

                switch (name)
                {
                    case "g":
                    case "a":
                        RenderChildren(element, style, target, inner, viewport);
                        break;
                    case "switch":
                        var first = element.Elements().FirstOrDefault();
                        if (first != null) Render(first, style, target, inner, viewport);
                        break;
                    case "svg":
                        RenderViewport(element, element, style, target, inner, viewport, null, null);
                        break;
                    case "use":
                        RenderUse(element, style, target, inner, viewport);
                        break;
                    default:
                        RenderShape(element, name, style.Visible ? leafStyle : null, target, inner, viewport);
                        break;
                }

                _formBase = formBase;

                if (group)
                {
                    var index = AddGroup(target.ToString(), inner);
                    output.AppendFormat("/GS{0} gs\n/Fm{1} Do\n", GraphicsState(style.Opacity, style.Opacity), index);
                }

                output.Append("Q\n");
            }
            finally
            {
                _depth--;
            }
        }

        private static void CopyPaint(SvgStyle from, SvgStyle to)
        {
            to.Fill = from.Fill;
            to.Stroke = from.Stroke;
        }

        /// <summary>
        /// Draws a nested svg element, or a symbol referred to by a use element: content with its
        /// own viewport, coordinate system and (by default) clipping
        /// </summary>
        private void RenderViewport(XElement element, XElement sizing, SvgStyle style, StringBuilder output, SvgMatrix ctm, (double W, double H) viewport, double? useWidth, double? useHeight)
        {
            var x = element.Name.LocalName == "symbol" ? 0 : Length((string)element.Attribute("x"), viewport.W) ?? 0;
            var y = element.Name.LocalName == "symbol" ? 0 : Length((string)element.Attribute("y"), viewport.H) ?? 0;
            var width = useWidth ?? Length((string)sizing.Attribute("width"), viewport.W) ?? viewport.W;
            var height = useHeight ?? Length((string)sizing.Attribute("height"), viewport.H) ?? viewport.H;
            if (width <= 0 || height <= 0) return;

            var viewBox = ParseViewBox((string)element.Attribute("viewBox"));

            output.Append("q\n");
            var overflow = Specified(element, "overflow") ?? "hidden";
            if (overflow == "hidden" || overflow == "scroll")
            {
                output.AppendFormat("{0} {1} {2} {3} re W n\n", SvgNumber.Format(x), SvgNumber.Format(y), SvgNumber.Format(width), SvgNumber.Format(height));
            }

            var transform = SvgMatrix.Translate(x, y);
            if (viewBox != null)
            {
                transform = transform.Multiply(ViewBoxTransform(viewBox, width, height, (string)element.Attribute("preserveAspectRatio")));
            }
            if (!transform.IsIdentity) output.Append(transform.ToPdf()).Append('\n');

            RenderChildren(element, style, output, ctm.Multiply(transform), viewBox != null ? (viewBox[2], viewBox[3]) : (width, height));
            output.Append("Q\n");
        }

        private void RenderUse(XElement use, SvgStyle style, StringBuilder output, SvgMatrix ctm, (double W, double H) viewport)
        {
            var href = (string)use.Attribute("href") ?? (string)use.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"));
            if (href == null || !href.StartsWith("#") || !_ids.TryGetValue(href.Substring(1), out var referenced)) return;

            if (referenced.Name.LocalName == "symbol")
            {
                var symbolStyle = ComputeStyle(referenced, style);
                if (!symbolStyle.Display) return;

                RenderViewport(referenced, use, symbolStyle, output, ctm, viewport,
                    Length((string)use.Attribute("width"), viewport.W), Length((string)use.Attribute("height"), viewport.H));
            }
            else
            {
                // the referenced element inherits from the use element, not from where it is defined
                Render(referenced, style, output, ctm, viewport);
            }
        }

        private void RenderShape(XElement element, string name, SvgStyle style, StringBuilder output, SvgMatrix ctm, (double W, double H) viewport)
        {
            if (style == null) return; // hidden

            var path = new StringBuilder();
            var writer = new SvgPathWriter(path);
            if (!WriteShapeGeometry(element, name, writer, viewport) || !writer.HasSegments) return;
            var bounds = writer.Bounds;

            // lines have no area, so can only be stroked
            var fill = name == "line" ? null : Simplify(ResolvePaint(style.Fill, style, viewport), bounds);
            var stroke = Simplify(ResolvePaint(style.Stroke, style, viewport), bounds);
            var strokeWidth = Length(style.StrokeWidth, Diagonal(viewport)) ?? 1;
            if (strokeWidth <= 0) stroke = null;
            if (fill == null && stroke == null) return;

            if (fill?.Gradient == null && stroke?.Gradient == null)
            {
                // solid colours: the fill and stroke are painted together
                var fillAlpha = fill == null ? 1 : style.FillOpacity * fill.Color.Alpha;
                var strokeAlpha = stroke == null ? 1 : style.StrokeOpacity * stroke.Color.Alpha;
                if (fill != null && fillAlpha <= 0) fill = null;
                if (stroke != null && strokeAlpha <= 0) stroke = null;
                if (fill == null && stroke == null) return;

                if (fillAlpha < 1 || strokeAlpha < 1)
                {
                    output.AppendFormat("/GS{0} gs\n", GraphicsState(fill != null ? fillAlpha : 1, stroke != null ? strokeAlpha : 1));
                }
                if (fill != null) output.Append(fill.Color.ToPdf("rg")).Append('\n');
                if (stroke != null)
                {
                    output.Append(stroke.Color.ToPdf("RG")).Append('\n');
                    WriteStroke(output, style, strokeWidth, viewport);
                }

                output.Append(path);
                if (fill != null && stroke != null) output.Append(style.FillEvenOdd ? "B*\n" : "B\n");
                else if (fill != null) output.Append(style.FillEvenOdd ? "f*\n" : "f\n");
                else output.Append("S\n");
                return;
            }

            // with a gradient, the fill and stroke are painted separately (fill first, as with
            // the B operator), as each can need its own pattern, opacity and soft mask
            if (fill != null)
            {
                var paint = new StringBuilder();
                if (SetPaint(paint, fill, false, style.FillOpacity, bounds, ctm))
                {
                    output.Append("q\n").Append(paint).Append(path).Append(style.FillEvenOdd ? "f*\n" : "f\n").Append("Q\n");
                }
            }

            if (stroke != null)
            {
                // the stroke extends past the geometry, which any soft mask has to cover
                var extra = strokeWidth * Math.Max(1, style.MiterLimit);
                var strokeBounds = new[] { bounds[0] - extra, bounds[1] - extra, bounds[2] + extra, bounds[3] + extra };

                var paint = new StringBuilder();
                if (SetPaint(paint, stroke, true, style.StrokeOpacity, bounds, ctm, strokeBounds))
                {
                    output.Append("q\n").Append(paint);
                    WriteStroke(output, style, strokeWidth, viewport);
                    output.Append(path).Append("S\nQ\n");
                }
            }
        }

        private void WriteStroke(StringBuilder output, SvgStyle style, double strokeWidth, (double W, double H) viewport)
        {
            output.Append(SvgNumber.Format(strokeWidth)).Append(" w\n");
            if (style.LineCap != 0) output.Append(style.LineCap).Append(" J\n");
            if (style.LineJoin != 0) output.Append(style.LineJoin).Append(" j\n");
            output.Append(SvgNumber.Format(style.MiterLimit)).Append(" M\n");

            var dashes = DashArray(style, viewport);
            if (dashes != null)
            {
                output.Append('[').Append(string.Join(" ", dashes.Select(SvgNumber.Format))).Append("] ")
                    .Append(SvgNumber.Format(Length(style.DashOffset, Diagonal(viewport)) ?? 0)).Append(" d\n");
            }
        }

        /// <summary>
        /// Sets the fill or stroke paint. Returns false if nothing would be visible.
        /// </summary>
        /// <param name="bounds">the shape's geometry, which bounding box units refer to</param>
        /// <param name="area">the area the paint has to cover (defaults to the geometry)</param>
        private bool SetPaint(StringBuilder output, SvgPaint paint, bool stroke, double opacity, double[] bounds, SvgMatrix ctm, double[] area = null)
        {
            area ??= bounds;

            if (paint.Gradient == null)
            {
                var alpha = opacity * paint.Color.Alpha;
                if (alpha <= 0) return false;
                if (alpha < 1) output.AppendFormat("/GS{0} gs\n", GraphicsState(stroke ? 1 : alpha, stroke ? alpha : 1));
                output.Append(paint.Color.ToPdf(stroke ? "RG" : "rg")).Append('\n');
                return true;
            }

            var gradient = paint.Gradient;

            // the gradient's co-ordinates map onto the shape's bounding box, or are in user space
            var units = gradient.ObjectBoundingBox
                ? new SvgMatrix(bounds[2] - bounds[0], 0, 0, bounds[3] - bounds[1], bounds[0], bounds[1])
                : SvgMatrix.Identity;
            var toUser = units.Multiply(gradient.Transform);

            // the area to cover, in the gradient's own co-ordinates
            var inverse = toUser.Invert();
            var corners = new[]
            {
                inverse.Apply(area[0], area[1]), inverse.Apply(area[2], area[1]),
                inverse.Apply(area[0], area[3]), inverse.Apply(area[2], area[3])
            };
            var gradientArea = new[] { corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y) };

            // the pattern is positioned relative to the form it is drawn in
            var matrix = _formBase.Invert().Multiply(ctm).Multiply(toUser);
            _drawing.Patterns.Add(string.Format("<< /Type /Pattern /PatternType 2 /Shading {0} /Matrix [{1}] >>",
                SvgShading.Build(gradient, false, gradientArea), matrix.ToNumbers()));
            var pattern = _drawing.Patterns.Count;

            if (gradient.UniformOpacity)
            {
                var alpha = opacity * gradient.Stops[0].Color.Alpha;
                if (alpha <= 0) return false;
                if (alpha < 1) output.AppendFormat("/GS{0} gs\n", GraphicsState(stroke ? 1 : alpha, stroke ? alpha : 1));
            }
            else
            {
                // PDF shadings have no opacity, so the stops' opacity is applied with a soft mask
                // drawn as a grey gradient (black is transparent, white opaque)
                _drawing.Shadings.Add(SvgShading.Build(gradient, true, gradientArea));
                var mask = AddForm(toUser.ToPdf() + "\n/Sh" + _drawing.Shadings.Count + " sh\n", area,
                    "<< /Type /Group /S /Transparency /CS /DeviceGray >>");

                output.AppendFormat("/GS{0} gs\n", GraphicsState(string.Format(
                    "<< /Type /ExtGState /SMask << /Type /Mask /S /Luminosity /G {{Fm{0}}} >> /ca {1} /CA {1} >>", mask, SvgNumber.Format(opacity))));
            }

            output.AppendFormat(stroke ? "/Pattern CS /P{0} SCN\n" : "/Pattern cs /P{0} scn\n", pattern);
            return true;
        }

        /// <summary>
        /// A fill or stroke: either a colour or a gradient
        /// </summary>
        private sealed class SvgPaint
        {
            public SvgColor Color;
            public SvgGradient Gradient;
        }

        /// <summary>
        /// Resolves a fill or stroke value. Returns null for none.
        /// </summary>
        private SvgPaint ResolvePaint(string value, SvgStyle style, (double W, double H) viewport)
        {
            value = value.Trim();
            if (value == "none") return null;

            if (value.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
            {
                var close = value.IndexOf(')');
                var fallback = close >= 0 ? value.Substring(close + 1).Trim() : "";
                var referenced = close >= 0 ? ResolveReference(value.Substring(0, close + 1)) : null;

                switch (referenced?.Name.LocalName)
                {
                    case "linearGradient":
                    case "radialGradient":
                        var gradient = ResolveGradient(referenced, viewport);
                        return gradient.Stops.Count == 0 ? null : new SvgPaint { Gradient = gradient };
                    case "pattern":
                        throw new NotSupportedException("SVG patterns are not supported yet.");
                }

                // a missing or invalid reference uses the fallback colour (if there is one)
                if (fallback.Length == 0 || fallback == "none") return null;
                value = fallback;
            }

            return new SvgPaint { Color = ResolveColor(value, style) };
        }

        /// <summary>
        /// Gradients that can't vary are drawn in a solid colour. A gradient using bounding box
        /// units on a shape with no width or height can't be drawn at all.
        /// </summary>
        private static SvgPaint Simplify(SvgPaint paint, double[] bounds)
        {
            var gradient = paint?.Gradient;
            if (gradient == null) return paint;

            if (gradient.ObjectBoundingBox && (bounds[2] - bounds[0] <= 0 || bounds[3] - bounds[1] <= 0)) return null;
            if (gradient.IsSolid) return new SvgPaint { Color = gradient.Stops[gradient.Stops.Count - 1].Color };
            return paint;
        }

        /// <summary>
        /// Reads a gradient element, along with anything it inherits through href
        /// </summary>
        private SvgGradient ResolveGradient(XElement element, (double W, double H) viewport)
        {
            var chain = new List<XElement>();
            for (var current = element; current != null && !chain.Contains(current); current = Referenced(current))
            {
                if (current.Name.LocalName != "linearGradient" && current.Name.LocalName != "radialGradient") break;
                chain.Add(current);
            }

            string Attribute(string name) => chain.Select(c => (string)c.Attribute(name)).FirstOrDefault(v => v != null);

            var gradient = new SvgGradient
            {
                Radial = element.Name.LocalName == "radialGradient",
                ObjectBoundingBox = Attribute("gradientUnits") != "userSpaceOnUse",
                Transform = SvgMatrix.Parse(Attribute("gradientTransform")),
                Spread = Attribute("spreadMethod") is string spread && (spread == "reflect" || spread == "repeat") ? spread : "pad"
            };

            // bounding box units are fractions of the box (percentages are fractions too)
            double Coordinate(string name, string fallback, double reference)
            {
                var value = Attribute(name) ?? fallback;
                return (gradient.ObjectBoundingBox ? Length(value, 1) : Length(value, reference)) ?? Length(fallback, gradient.ObjectBoundingBox ? 1 : reference) ?? 0;
            }

            if (gradient.Radial)
            {
                gradient.Cx = Coordinate("cx", "50%", viewport.W);
                gradient.Cy = Coordinate("cy", "50%", viewport.H);
                gradient.R = Coordinate("r", "50%", Diagonal(viewport));
                gradient.Fx = Attribute("fx") != null ? Coordinate("fx", "50%", viewport.W) : gradient.Cx;
                gradient.Fy = Attribute("fy") != null ? Coordinate("fy", "50%", viewport.H) : gradient.Cy;
                gradient.Fr = Coordinate("fr", "0%", Diagonal(viewport));
            }
            else
            {
                gradient.X1 = Coordinate("x1", "0%", viewport.W);
                gradient.Y1 = Coordinate("y1", "0%", viewport.H);
                gradient.X2 = Coordinate("x2", "100%", viewport.W);
                gradient.Y2 = Coordinate("y2", "0%", viewport.H);
            }

            // the stops come from the first gradient in the chain that has any
            var holder = chain.FirstOrDefault(c => c.Elements().Any(e => e.Name.LocalName == "stop"));
            if (holder != null)
            {
                // stops inherit (eg: color, for currentColor) from where the gradient is defined
                var holderStyle = InheritedStyle(holder);
                var previous = 0.0;
                foreach (var stop in holder.Elements().Where(e => e.Name.LocalName == "stop"))
                {
                    var style = ComputeStyle(stop, holderStyle);
                    var offsetText = ((string)stop.Attribute("offset") ?? "0").Trim();
                    var offset = Length(offsetText, 1) ?? 0;

                    // offsets are clamped to 0..1 and can't go backwards
                    offset = Math.Max(previous, Math.Clamp(offset, 0, 1));
                    previous = offset;

                    var color = ResolveColor(style.StopColor, style);
                    gradient.Stops.Add((offset, new SvgColor(color.R, color.G, color.B, color.Alpha * style.StopOpacity)));
                }
            }

            return gradient;
        }

        /// <summary>
        /// The style of an element computed through its ancestors, for elements (like gradients)
        /// that are used from somewhere other than where they are defined
        /// </summary>
        private SvgStyle InheritedStyle(XElement element)
        {
            var parent = element.Parent == null ? new SvgStyle() : InheritedStyle(element.Parent);
            return ComputeStyle(element, parent);
        }

        private XElement Referenced(XElement element)
        {
            var href = (string)element.Attribute("href") ?? (string)element.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"));
            return href != null && href.StartsWith("#") && _ids.TryGetValue(href.Substring(1), out var referenced) ? referenced : null;
        }

        /// <summary>
        /// Writes the outline of a shape element. Returns false if the shape has nothing to draw.
        /// </summary>
        private bool WriteShapeGeometry(XElement element, string name, SvgPathWriter writer, (double W, double H) viewport)
        {
            double L(string attribute, double reference) => Length((string)element.Attribute(attribute), reference) ?? 0;

            switch (name)
            {
                case "path":
                    SvgPathData.Write((string)element.Attribute("d"), writer);
                    return true;

                case "rect":
                {
                    var width = L("width", viewport.W);
                    var height = L("height", viewport.H);
                    if (width <= 0 || height <= 0) return false;

                    var rx = Length((string)element.Attribute("rx"), viewport.W);
                    var ry = Length((string)element.Attribute("ry"), viewport.H);
                    rx ??= ry;
                    ry ??= rx;
                    SvgPathData.Rectangle(writer, L("x", viewport.W), L("y", viewport.H), width, height,
                        Math.Min(rx ?? 0, width / 2), Math.Min(ry ?? 0, height / 2));
                    return true;
                }

                case "circle":
                {
                    var r = L("r", Diagonal(viewport));
                    if (r <= 0) return false;
                    SvgPathData.Ellipse(writer, L("cx", viewport.W), L("cy", viewport.H), r, r);
                    return true;
                }

                case "ellipse":
                {
                    var rx = Length((string)element.Attribute("rx"), viewport.W);
                    var ry = Length((string)element.Attribute("ry"), viewport.H);
                    rx ??= ry;
                    ry ??= rx;
                    if (rx == null || rx <= 0 || ry <= 0) return false;
                    SvgPathData.Ellipse(writer, L("cx", viewport.W), L("cy", viewport.H), rx.Value, ry.Value);
                    return true;
                }

                case "line":
                    writer.MoveTo(L("x1", viewport.W), L("y1", viewport.H));
                    writer.LineTo(L("x2", viewport.W), L("y2", viewport.H));
                    return true;

                case "polyline":
                case "polygon":
                {
                    var points = SvgNumber.ParseList((string)element.Attribute("points"));
                    if (points.Count < 2) return false;

                    writer.MoveTo(points[0], points[1]);
                    for (var i = 2; i + 1 < points.Count; i += 2)
                    {
                        writer.LineTo(points[i], points[i + 1]);
                    }
                    if (name == "polygon") writer.Close();
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// Writes the clipping path an element refers to. Returns false if it is empty.
        /// </summary>
        private bool WriteClipPath(string reference, StringBuilder output, (double W, double H) viewport)
        {
            var clip = ResolveReference(reference);
            if (clip == null || clip.Name.LocalName != "clipPath") return true; // an invalid reference is ignored

            if ((string)clip.Attribute("clipPathUnits") == "objectBoundingBox")
            {
                throw new NotSupportedException("SVG clipping paths using objectBoundingBox units are not supported yet.");
            }

            var clipTransform = SvgMatrix.Parse((string)clip.Attribute("transform"));
            var clipStyle = ComputeStyle(clip, new SvgStyle());
            var path = new StringBuilder();
            var evenOdd = false;
            var any = false;

            foreach (var child in clip.Elements())
            {
                var name = child.Name.LocalName;
                var target = child;
                var childTransform = SvgMatrix.Parse((string)child.Attribute("transform"));

                if (name == "use")
                {
                    var href = (string)child.Attribute("href") ?? (string)child.Attribute(XName.Get("href", "http://www.w3.org/1999/xlink"));
                    if (href == null || !href.StartsWith("#") || !_ids.TryGetValue(href.Substring(1), out target)) continue;
                    childTransform = childTransform
                        .Multiply(SvgMatrix.Translate(Length((string)child.Attribute("x"), viewport.W) ?? 0, Length((string)child.Attribute("y"), viewport.H) ?? 0))
                        .Multiply(SvgMatrix.Parse((string)target.Attribute("transform")));
                    name = target.Name.LocalName;
                }

                var style = ComputeStyle(target, clipStyle);
                if (!style.Display || !style.Visible) continue;

                // the shapes are combined into one path, so their transforms are applied to the points
                var writer = new SvgPathWriter(path, clipTransform.Multiply(childTransform));
                if (WriteShapeGeometry(target, name, writer, viewport))
                {
                    if (!any) evenOdd = style.ClipEvenOdd;
                    any = true;
                }
            }

            if (!any || path.Length == 0) return false;

            output.Append(path).Append(evenOdd ? "W* n\n" : "W n\n");
            return true;
        }

        #endregion

        #region Styles

        /// <summary>
        /// Works out the style of an element: presentation attributes, then matching CSS rules,
        /// then the style attribute (with !important CSS declarations last)
        /// </summary>
        private SvgStyle ComputeStyle(XElement element, SvgStyle parent)
        {
            var style = parent.Inherit();

            foreach (var attribute in element.Attributes())
            {
                var property = attribute.Name.LocalName;
                if (attribute.Name.NamespaceName == "" && SvgStyle.Properties.Contains(property))
                {
                    style.Set(property, attribute.Value, parent);
                }
            }

            var important = new List<(string Property, string Value)>();
            foreach (var (property, value, isImportant) in _styleSheet.Match(element))
            {
                if (isImportant) important.Add((property, value));
                else style.Set(property, value, parent);
            }

            foreach (var (property, value, isImportant) in SvgStyleSheet.ParseDeclarations((string)element.Attribute("style")))
            {
                style.Set(property, value, parent);
            }

            foreach (var (property, value) in important)
            {
                style.Set(property, value, parent);
            }

            return style;
        }

        /// <summary>
        /// The specified value of a property that isn't handled by SvgStyle (eg: overflow)
        /// </summary>
        private static string Specified(XElement element, string property)
        {
            foreach (var (name, value, _) in SvgStyleSheet.ParseDeclarations((string)element.Attribute("style")))
            {
                if (name == property) return value;
            }
            return (string)element.Attribute(property);
        }

        private static void CheckSupported(SvgStyle style, string element)
        {
            if (style.Mask != "none") throw new NotSupportedException("SVG masks are not supported yet.");
            if (style.Filter != "none") throw new NotSupportedException("SVG filters are not supported yet.");
            if (style.Marker != "none") throw new NotSupportedException("SVG markers are not supported yet.");
        }

        /// <summary>
        /// True if a fill or stroke value paints anything
        /// </summary>
        private static bool Paints(string paint, SvgStyle style)
        {
            return paint.Trim() != "none";
        }

        private static SvgColor ResolveColor(string paint, SvgStyle style)
        {
            paint = paint.Trim();
            if (paint.Equals("currentColor", StringComparison.OrdinalIgnoreCase))
            {
                paint = style.Color;
            }
            if (!SvgColor.TryParse(paint, out var color))
            {
                // an invalid colour is ignored, leaving the default (black)
                return SvgColor.Black;
            }
            return color;
        }

        private double[] DashArray(SvgStyle style, (double W, double H) viewport)
        {
            if (style.DashArray == "none") return null;

            var values = style.DashArray.Split(new[] { ',', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(v => Length(v, Diagonal(viewport)) ?? -1)
                .ToArray();

            // negative values, or a pattern with no length, mean the line is solid
            if (values.Length == 0 || values.Any(v => v < 0) || values.Sum() <= 0) return null;
            return values;
        }

        private XElement ResolveReference(string value)
        {
            value = value.Trim();
            if (!value.StartsWith("url(", StringComparison.OrdinalIgnoreCase)) return null;
            var id = value.Substring(4).TrimEnd(')').Trim().Trim('"', '\'').TrimStart('#');
            return _ids.TryGetValue(id, out var element) ? element : null;
        }

        private int GraphicsState(double fill, double stroke)
        {
            return GraphicsState(string.Format("<< /Type /ExtGState /ca {0} /CA {1} >>", SvgNumber.Format(fill), SvgNumber.Format(stroke)));
        }

        private int GraphicsState(string dictionary)
        {
            if (!_graphicsStates.TryGetValue(dictionary, out var index))
            {
                _drawing.GraphicsStates.Add(dictionary);
                index = _drawing.GraphicsStates.Count;
                _graphicsStates[dictionary] = index;
            }
            return index;
        }

        /// <summary>
        /// Adds a transparency group. Its bounding box is the image's area, expressed in the
        /// co-ordinate system the group is drawn in.
        /// </summary>
        private int AddGroup(string content, SvgMatrix ctm)
        {
            var inverse = ctm.Invert();
            var corners = new[]
            {
                inverse.Apply(0, 0), inverse.Apply(_drawing.Width, 0),
                inverse.Apply(0, _drawing.Height), inverse.Apply(_drawing.Width, _drawing.Height)
            };
            var bbox = new[] { corners.Min(c => c.X), corners.Min(c => c.Y), corners.Max(c => c.X), corners.Max(c => c.Y) };

            return AddForm(content, bbox, "<< /Type /Group /S /Transparency >>");
        }

        private int AddForm(string content, double[] bbox, string group)
        {
            _drawing.Forms.Add(new SvgForm { Content = content, BBox = bbox, Group = group });
            return _drawing.Forms.Count;
        }

        #endregion

        #region Lengths and viewports

        private static double Diagonal((double W, double H) viewport)
        {
            return Math.Sqrt(((viewport.W * viewport.W) + (viewport.H * viewport.H)) / 2);
        }

        /// <summary>
        /// Reads a length in CSS pixels, eg: "10", "2.5mm", "50%". Returns null if missing or invalid.
        /// </summary>
        private static double? Length(string value, double reference, bool allowPercent = true)
        {
            if (string.IsNullOrWhiteSpace(value)) return null;
            value = value.Trim();

            var pos = 0;
            if (!SvgNumber.TryRead(value, ref pos, out var number)) return null;

            switch (value.Substring(pos).Trim().ToLowerInvariant())
            {
                case "":
                case "px": return number;
                case "pt": return number * 96 / 72;
                case "pc": return number * 16;
                case "mm": return number * 96 / 25.4;
                case "cm": return number * 96 / 2.54;
                case "in": return number * 96;
                case "em": return number * 16;
                case "ex": return number * 8;
                case "%": return allowPercent ? number * reference / 100 : (double?)null;
                default: return null;
            }
        }

        private static double[] ParseViewBox(string value)
        {
            var numbers = SvgNumber.ParseList(value);
            if (numbers.Count != 4 || numbers[2] <= 0 || numbers[3] <= 0) return null;
            return numbers.ToArray();
        }

        /// <summary>
        /// Maps a viewBox onto a viewport, following preserveAspectRatio (default: xMidYMid meet)
        /// </summary>
        private static SvgMatrix ViewBoxTransform(double[] viewBox, double width, double height, string preserveAspectRatio)
        {
            var parts = (preserveAspectRatio ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries).ToList();
            if (parts.Count > 0 && parts[0] == "defer") parts.RemoveAt(0);
            var align = parts.Count > 0 ? parts[0] : "xMidYMid";
            var slice = parts.Count > 1 && parts[1] == "slice";

            var sx = width / viewBox[2];
            var sy = height / viewBox[3];

            if (align == "none")
            {
                return new SvgMatrix(sx, 0, 0, sy, -viewBox[0] * sx, -viewBox[1] * sy);
            }

            var scale = slice ? Math.Max(sx, sy) : Math.Min(sx, sy);
            var tx = -viewBox[0] * scale;
            var ty = -viewBox[1] * scale;

            if (align.Contains("xMid")) tx += (width - (viewBox[2] * scale)) / 2;
            else if (align.Contains("xMax")) tx += width - (viewBox[2] * scale);
            if (align.Contains("YMid")) ty += (height - (viewBox[3] * scale)) / 2;
            else if (align.Contains("YMax")) ty += height - (viewBox[3] * scale);

            return new SvgMatrix(scale, 0, 0, scale, tx, ty);
        }

        #endregion
    }
}
