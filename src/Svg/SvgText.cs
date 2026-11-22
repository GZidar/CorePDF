using CorePDF.TypeFaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Xml.Linq;

namespace CorePDF.Svg
{
    /// <summary>
    /// Lays out and writes SVG text (text and tspan elements) using the document's fonts.
    /// </summary>
    internal sealed partial class SvgConverter
    {
        /// <summary>
        /// A character to be drawn, with the style and position attributes that apply to it
        /// </summary>
        private sealed class TextCharacter
        {
            public string Text;
            public SvgStyle Style;
            public double? X, Y;
            public double Dx, Dy;
        }

        /// <summary>
        /// A positioned character
        /// </summary>
        private sealed class Glyph
        {
            public string Text;
            public SvgStyle Style;
            public Font Font;
            public double Size;
            public double X, Y;
            public double Advance;
        }

        // the font families that map onto each of the standard PDF fonts
        private static readonly HashSet<string> SansSerifFamilies = new HashSet<string>
        {
            "sans-serif", "helvetica", "arial", "helvetica neue", "arial nova", "liberation sans", "nimbus sans",
            "system-ui", "-apple-system", "segoe ui", "verdana", "tahoma", "roboto", "open sans", "calibri"
        };
        private static readonly HashSet<string> SerifFamilies = new HashSet<string>
        {
            "serif", "times", "times new roman", "georgia", "liberation serif", "nimbus roman", "cambria"
        };
        private static readonly HashSet<string> MonospaceFamilies = new HashSet<string>
        {
            "monospace", "courier", "courier new", "consolas", "menlo", "monaco", "liberation mono", "lucida console"
        };

        private void RenderText(XElement element, SvgStyle style, StringBuilder output, SvgMatrix ctm, (double W, double H) viewport)
        {
            if (style == null) return; // hidden

            if (_fonts == null)
            {
                throw new NotSupportedException("SVG text can only be drawn when the image is part of a document, as it uses the document's fonts.");
            }

            var characters = CollectCharacters(element, style, viewport);
            if (characters.Count == 0) return;

            var glyphs = Layout(characters, viewport);
            var bounds = TextBounds(glyphs);

            // runs of glyphs that share a font, size, style and baseline are written together
            var runs = new List<List<Glyph>>();
            foreach (var glyph in glyphs)
            {
                var run = runs.Count > 0 ? runs[runs.Count - 1] : null;
                if (run == null || run[0].Font != glyph.Font || run[0].Size != glyph.Size || run[0].Style != glyph.Style || run[0].Y != glyph.Y)
                {
                    run = new List<Glyph>();
                    runs.Add(run);
                }
                run.Add(glyph);
            }

            foreach (var run in runs)
            {
                var runStyle = run[0].Style;
                if (!runStyle.Visible) continue;

                var fill = Simplify(ResolvePaint(runStyle.Fill, runStyle, viewport), bounds);
                var stroke = Simplify(ResolvePaint(runStyle.Stroke, runStyle, viewport), bounds);
                var strokeWidth = Length(runStyle.StrokeWidth, Diagonal(viewport)) ?? 1;
                if (strokeWidth <= 0) stroke = null;
                if (fill == null && stroke == null) continue;

                var text = TextOperators(run);

                if (fill?.Gradient == null && stroke?.Gradient == null)
                {
                    // solid colours: drawn with a single text rendering mode
                    var fillAlpha = fill == null ? 1 : runStyle.FillOpacity * fill.Color.Alpha;
                    var strokeAlpha = stroke == null ? 1 : runStyle.StrokeOpacity * stroke.Color.Alpha;
                    if (fill != null && fillAlpha <= 0) fill = null;
                    if (stroke != null && strokeAlpha <= 0) stroke = null;
                    if (fill == null && stroke == null) continue;

                    output.Append("q\n");
                    if (fillAlpha < 1 || strokeAlpha < 1)
                    {
                        output.AppendFormat("/GS{0} gs\n", GraphicsState(fill != null ? fillAlpha : 1, stroke != null ? strokeAlpha : 1));
                    }
                    if (fill != null) output.Append(fill.Color.ToPdf("rg")).Append('\n');
                    if (stroke != null)
                    {
                        output.Append(stroke.Color.ToPdf("RG")).Append('\n');
                        WriteStroke(output, runStyle, strokeWidth, viewport);
                    }
                    WriteText(output, text, fill != null ? (stroke != null ? 2 : 0) : 1);
                    output.Append("Q\n");
                    continue;
                }

                // gradients: the fill and stroke are drawn separately, each with its own paint
                if (fill != null)
                {
                    var paint = new StringBuilder();
                    if (SetPaint(paint, fill, false, runStyle.FillOpacity, bounds, ctm))
                    {
                        output.Append("q\n").Append(paint);
                        WriteText(output, text, 0);
                        output.Append("Q\n");
                    }
                }
                if (stroke != null)
                {
                    var extra = strokeWidth * Math.Max(1, runStyle.MiterLimit);
                    var area = new[] { bounds[0] - extra, bounds[1] - extra, bounds[2] + extra, bounds[3] + extra };
                    var paint = new StringBuilder();
                    if (SetPaint(paint, stroke, true, runStyle.StrokeOpacity, bounds, ctm, area))
                    {
                        output.Append("q\n").Append(paint);
                        WriteStroke(output, runStyle, strokeWidth, viewport);
                        WriteText(output, text, 1);
                        output.Append("Q\n");
                    }
                }
            }
        }

        private static void WriteText(StringBuilder output, string text, int renderingMode)
        {
            output.Append("BT\n");
            if (renderingMode != 0) output.Append(renderingMode).Append(" Tr\n");
            output.Append(text).Append("ET\n");
        }

        /// <summary>
        /// The text operators for a run of glyphs. The text matrix flips the glyphs upright, as
        /// the drawing uses SVG's co-ordinates (with y going down the page); differences from
        /// the font's own spacing are written as adjustments in a TJ array.
        /// </summary>
        private string TextOperators(List<Glyph> run)
        {
            var first = run[0];
            if (!_usedFonts.Contains(first.Font)) _usedFonts.Add(first.Font);

            var result = new StringBuilder();
            result.AppendFormat("/{0} {1} Tf\n", first.Font.Id, SvgNumber.Format(first.Size));
            result.AppendFormat("1 0 0 -1 {0} {1} Tm\n", SvgNumber.Format(first.X), SvgNumber.Format(first.Y));

            var parts = new List<string>();
            var pending = new StringBuilder();
            var expected = first.X;

            foreach (var glyph in run)
            {
                var offset = glyph.X - expected;
                if (Math.Abs(offset) > 0.0001)
                {
                    if (pending.Length > 0)
                    {
                        parts.Add(glyph.Font.EncodeText(pending.ToString()));
                        pending.Clear();
                    }
                    // TJ adjustments are in thousandths of the font size, with positive values moving left
                    parts.Add(SvgNumber.Format(-offset * 1000 / glyph.Size));
                }
                pending.Append(glyph.Text);
                expected = glyph.X + glyph.Advance;
            }
            if (pending.Length > 0)
            {
                parts.Add(first.Font.EncodeText(pending.ToString()));
            }

            result.Append('[').Append(string.Join(" ", parts)).Append("] TJ\n");
            return result.ToString();
        }

        /// <summary>
        /// Gathers the characters of a text element (and its tspans), collapsing whitespace as
        /// browsers do, and works out which position attributes apply to each one
        /// </summary>
        private List<TextCharacter> CollectCharacters(XElement text, SvgStyle style, (double W, double H) viewport)
        {
            var segments = new List<(string Text, SvgStyle Style, List<XElement> Chain, bool Preserve)>();

            void Collect(XElement element, SvgStyle elementStyle, List<XElement> chain, bool preserve)
            {
                var space = (string)element.Attribute(XNamespace.Xml + "space");
                if (space == "preserve") preserve = true;
                else if (space == "default") preserve = false;

                if (element.Attribute("rotate") != null)
                {
                    throw new NotSupportedException("Rotated characters in SVG text (the rotate attribute) are not supported yet.");
                }

                foreach (var node in element.Nodes())
                {
                    if (node is XText textNode)
                    {
                        segments.Add((textNode.Value, elementStyle, chain, preserve));
                    }
                    else if (node is XElement child && (child.Name.NamespaceName == "" || child.Name.NamespaceName == "http://www.w3.org/2000/svg"))
                    {
                        switch (child.Name.LocalName)
                        {
                            case "tspan":
                            case "a":
                                var childStyle = ComputeStyle(child, elementStyle);
                                if (childStyle.Display)
                                {
                                    Collect(child, childStyle, new List<XElement>(chain) { child }, preserve);
                                }
                                break;
                            case "textPath":
                                throw new NotSupportedException("SVG text on a path (textPath) is not supported yet.");
                        }
                    }
                }
            }

            Collect(text, style, new List<XElement> { text }, false);

            // whitespace: line breaks and tabs become spaces, and runs of spaces collapse into
            // one, with none at the start or end (unless xml:space="preserve")
            var characters = new List<TextCharacter>();
            var chains = new List<List<XElement>>();
            var previousSpace = true;
            foreach (var (value, segmentStyle, chain, preserve) in segments)
            {
                foreach (var rune in value.EnumerateRunes())
                {
                    var c = rune.ToString();
                    var isSpace = c == " " || c == "\n" || c == "\r" || c == "\t";
                    if (isSpace)
                    {
                        if (!preserve && previousSpace) continue;
                        c = " ";
                    }
                    previousSpace = isSpace && !preserve;

                    characters.Add(new TextCharacter { Text = c, Style = segmentStyle });
                    chains.Add(chain);
                }
            }
            while (characters.Count > 0 && characters[characters.Count - 1].Text == " " && !IsPreserved(chains[chains.Count - 1]))
            {
                characters.RemoveAt(characters.Count - 1);
                chains.RemoveAt(chains.Count - 1);
            }

            // each value in a position list applies to a character by its index within the element
            // that has the list; the nearest element with a value for the character wins
            var lists = new Dictionary<XElement, (List<double> X, List<double> Y, List<double> Dx, List<double> Dy)>();
            var counts = new Dictionary<XElement, int>();
            for (var i = 0; i < characters.Count; i++)
            {
                var character = characters[i];
                var chain = chains[i];
                bool foundX = false, foundY = false, foundDx = false, foundDy = false;

                for (var level = chain.Count - 1; level >= 0; level--)
                {
                    var element = chain[level];
                    if (!lists.TryGetValue(element, out var positions))
                    {
                        positions = (Lengths(element, "x", viewport.W), Lengths(element, "y", viewport.H), Lengths(element, "dx", viewport.W), Lengths(element, "dy", viewport.H));
                        lists[element] = positions;
                    }
                    counts.TryGetValue(element, out var index);

                    if (!foundX && index < positions.X.Count) { character.X = positions.X[index]; foundX = true; }
                    if (!foundY && index < positions.Y.Count) { character.Y = positions.Y[index]; foundY = true; }
                    if (!foundDx && index < positions.Dx.Count) { character.Dx = positions.Dx[index]; foundDx = true; }
                    if (!foundDy && index < positions.Dy.Count) { character.Dy = positions.Dy[index]; foundDy = true; }
                }

                foreach (var element in chain)
                {
                    counts.TryGetValue(element, out var count);
                    counts[element] = count + 1;
                }
            }

            return characters;
        }

        private static bool IsPreserved(List<XElement> chain)
        {
            for (var i = chain.Count - 1; i >= 0; i--)
            {
                var space = (string)chain[i].Attribute(XNamespace.Xml + "space");
                if (space != null) return space == "preserve";
            }
            return false;
        }

        private static List<double> Lengths(XElement element, string attribute, double reference)
        {
            var value = (string)element.Attribute(attribute);
            var result = new List<double>();
            if (string.IsNullOrWhiteSpace(value)) return result;

            foreach (var part in value.Split(new[] { ',', ' ', '\t', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
            {
                result.Add(Length(part, reference) ?? 0);
            }
            return result;
        }

        /// <summary>
        /// Positions the characters: they advance along the baseline from each absolute position,
        /// then each chunk of text (starting at an absolute position) is aligned by its text-anchor
        /// </summary>
        private List<Glyph> Layout(List<TextCharacter> characters, (double W, double H) viewport)
        {
            var glyphs = new List<Glyph>();
            var chunks = new List<List<Glyph>>();
            double x = 0, y = 0;

            foreach (var character in characters)
            {
                var font = ResolveFont(character.Style);
                var size = character.Style.FontSize;

                if (character.X.HasValue || character.Y.HasValue || chunks.Count == 0)
                {
                    chunks.Add(new List<Glyph>());
                }
                x = (character.X ?? x) + character.Dx;
                y = (character.Y ?? y) + character.Dy;

                var advance = font.MeasureText(character.Text) * size / 1000;
                var glyph = new Glyph { Text = character.Text, Style = character.Style, Font = font, Size = size, X = x, Y = y, Advance = advance };
                glyphs.Add(glyph);
                chunks[chunks.Count - 1].Add(glyph);

                x += advance + Spacing(character.Style.LetterSpacing, size, viewport);
                if (character.Text == " ") x += Spacing(character.Style.WordSpacing, size, viewport);
            }

            foreach (var chunk in chunks.Where(c => c.Count > 0))
            {
                var anchor = chunk[0].Style.TextAnchor;
                if (anchor != "middle" && anchor != "end") continue;

                var last = chunk[chunk.Count - 1];
                var width = last.X + last.Advance - chunk[0].X;
                var shift = anchor == "end" ? -width : -width / 2;
                foreach (var glyph in chunk) glyph.X += shift;
            }

            foreach (var glyph in glyphs)
            {
                glyph.Y += BaselineShift(glyph);
            }

            return glyphs;
        }

        private static double Spacing(string value, double fontSize, (double W, double H) viewport)
        {
            if (value == "normal") return 0;
            if (value.EndsWith("em")) return (Length(value.Substring(0, value.Length - 2), 0) ?? 0) * fontSize;
            return Length(value, Diagonal(viewport)) ?? 0;
        }

        /// <summary>
        /// How far below the given position the glyph's (alphabetic) baseline is, for the
        /// dominant-baseline property
        /// </summary>
        private static double BaselineShift(Glyph glyph)
        {
            var scale = glyph.Size / 1000;
            switch (glyph.Style.DominantBaseline)
            {
                case "middle": return glyph.Font.XHeight / 2.0 * scale;
                case "central": return (glyph.Font.Ascent + glyph.Font.Descent) / 2.0 * scale;
                case "hanging": return glyph.Font.Ascent * 0.8 * scale;
                case "text-top":
                case "text-before-edge": return glyph.Font.Ascent * scale;
                case "text-bottom":
                case "text-after-edge":
                case "ideographic": return glyph.Font.Descent * scale;
                default: return 0;
            }
        }

        /// <summary>
        /// The area covered by the text: [left, top, right, bottom]
        /// </summary>
        private static double[] TextBounds(List<Glyph> glyphs)
        {
            return new[]
            {
                glyphs.Min(g => g.X),
                glyphs.Min(g => g.Y - (g.Font.Ascent * g.Size / 1000)),
                glyphs.Max(g => g.X + g.Advance),
                glyphs.Max(g => g.Y - (g.Font.Descent * g.Size / 1000))
            };
        }

        /// <summary>
        /// Finds the font for the text's font-family, weight and style. Embedded fonts are
        /// matched by name (their Name or BaseFont), common font names and the generic families
        /// map to the standard PDF fonts, and anything else falls back to the browser default
        /// (a serif font).
        /// </summary>
        private Font ResolveFont(SvgStyle style)
        {
            foreach (var family in style.FontFamily.Split(',').Select(f => f.Trim().Trim('"', '\'').Trim()).Where(f => f.Length > 0))
            {
                var embedded = _fonts.Where(f => f.IsEmbedded &&
                    (string.Equals(f.FontName, family, StringComparison.OrdinalIgnoreCase) || string.Equals(f.BaseFont, family, StringComparison.OrdinalIgnoreCase))).ToList();
                if (embedded.Count > 0)
                {
                    var baseFont = embedded[0].BaseFont;
                    return _fonts.FirstOrDefault(f => f.IsEmbedded && f.BaseFont == baseFont && f.Bold == style.Bold && f.Italic == style.Italic)
                        ?? embedded.FirstOrDefault(f => !f.Bold && !f.Italic)
                        ?? embedded[0];
                }

                var lower = family.ToLowerInvariant();
                if (SansSerifFamilies.Contains(lower)) return StandardFont(Fonts.FONTSANSSERIF, style);
                if (SerifFamilies.Contains(lower)) return StandardFont(Fonts.FONTSERIF, style);
                if (MonospaceFamilies.Contains(lower)) return StandardFont(Fonts.FONTFIXED, style);
            }

            return StandardFont(Fonts.FONTSERIF, style);
        }

        private Font StandardFont(string baseFont, SvgStyle style)
        {
            return _fonts.First(f => !f.IsEmbedded && f.BaseFont == baseFont && f.Bold == style.Bold && f.Italic == style.Italic);
        }
    }
}
