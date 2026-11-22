using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Xml.Linq;

namespace CorePDF.Svg
{
    /// <summary>
    /// The CSS rules from an SVG's &lt;style&gt; elements. Supports type, class and id selectors
    /// (including compound selectors like rect.big#main), descendant and child combinators,
    /// and selector lists. Rules using anything else (attribute selectors, pseudo-classes,
    /// etc) are ignored, as are at-rules such as @media.
    /// </summary>
    internal sealed class SvgStyleSheet
    {
        private sealed class Compound
        {
            public string Tag;
            public string Id;
            public List<string> Classes = new List<string>();
            public bool ChildOfPrevious; // joined to the previous compound with '>'
        }

        private sealed class Rule
        {
            public List<Compound> Selector;
            public int Specificity;
            public int Order;
            public List<(string Property, string Value, bool Important)> Declarations;
        }

        private readonly List<Rule> _rules = new List<Rule>();

        public void Add(string css)
        {
            if (string.IsNullOrWhiteSpace(css)) return;

            css = Regex.Replace(css, @"/\*.*?\*/", "", RegexOptions.Singleline);

            var pos = 0;
            while (pos < css.Length)
            {
                var open = css.IndexOf('{', pos);
                if (open < 0) break;
                var prelude = css.Substring(pos, open - pos).Trim();

                // find the matching close brace (at-rules like @media contain nested blocks)
                var depth = 0;
                var close = open;
                for (; close < css.Length; close++)
                {
                    if (css[close] == '{') depth++;
                    else if (css[close] == '}' && --depth == 0) break;
                }
                var body = css.Substring(open + 1, Math.Max(0, close - open - 1));
                pos = close + 1;

                if (prelude.StartsWith("@")) continue;

                var declarations = ParseDeclarations(body);
                foreach (var selectorText in prelude.Split(','))
                {
                    var selector = ParseSelector(selectorText.Trim());
                    if (selector == null) continue;

                    _rules.Add(new Rule
                    {
                        Selector = selector,
                        Specificity = (selector.Count(c => c.Id != null) * 10000) + (selector.Sum(c => c.Classes.Count) * 100) + selector.Count(c => c.Tag != null),
                        Order = _rules.Count,
                        Declarations = declarations
                    });
                }
            }
        }

        /// <summary>
        /// Parses CSS declarations, eg: "fill: red; stroke-width: 2px !important"
        /// </summary>
        public static List<(string Property, string Value, bool Important)> ParseDeclarations(string text)
        {
            var result = new List<(string, string, bool)>();
            if (string.IsNullOrWhiteSpace(text)) return result;

            foreach (var declaration in text.Split(';'))
            {
                var colon = declaration.IndexOf(':');
                if (colon <= 0) continue;

                var property = declaration.Substring(0, colon).Trim().ToLowerInvariant();
                var value = declaration.Substring(colon + 1).Trim();
                var important = false;
                var bang = value.IndexOf("!important", StringComparison.OrdinalIgnoreCase);
                if (bang >= 0)
                {
                    important = true;
                    value = value.Substring(0, bang).Trim();
                }
                if (property.Length > 0 && value.Length > 0)
                {
                    result.Add((property, value, important));
                }
            }
            return result;
        }

        /// <summary>
        /// The declarations that apply to the element, in increasing order of precedence
        /// </summary>
        public IEnumerable<(string Property, string Value, bool Important)> Match(XElement element)
        {
            return _rules
                .Where(r => Matches(r.Selector, r.Selector.Count - 1, element))
                .OrderBy(r => r.Specificity)
                .ThenBy(r => r.Order)
                .SelectMany(r => r.Declarations);
        }

        private static bool Matches(List<Compound> selector, int index, XElement element)
        {
            if (!MatchesCompound(selector[index], element)) return false;
            if (index == 0) return true;

            if (selector[index].ChildOfPrevious)
            {
                return element.Parent != null && Matches(selector, index - 1, element.Parent);
            }

            for (var ancestor = element.Parent; ancestor != null; ancestor = ancestor.Parent)
            {
                if (Matches(selector, index - 1, ancestor)) return true;
            }
            return false;
        }

        private static bool MatchesCompound(Compound compound, XElement element)
        {
            if (compound.Tag != null && compound.Tag != element.Name.LocalName) return false;
            if (compound.Id != null && compound.Id != (string)element.Attribute("id")) return false;

            if (compound.Classes.Count > 0)
            {
                var classes = ((string)element.Attribute("class") ?? "").Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
                if (!compound.Classes.All(c => classes.Contains(c))) return false;
            }
            return true;
        }

        private static readonly Regex CompoundPart = new Regex(@"\G(?:(?<tag>[A-Za-z][\w-]*)|(?<all>\*)|\.(?<class>[\w-]+)|#(?<id>[\w-]+))");

        private static List<Compound> ParseSelector(string text)
        {
            if (text.Length == 0) return null;

            var result = new List<Compound>();
            var child = false;
            foreach (var part in Regex.Split(text.Replace(">", " > "), @"\s+").Where(p => p.Length > 0))
            {
                if (part == ">")
                {
                    child = true;
                    continue;
                }

                var compound = new Compound { ChildOfPrevious = child };
                child = false;

                var pos = 0;
                while (pos < part.Length)
                {
                    var match = CompoundPart.Match(part, pos);
                    if (!match.Success) return null; // a selector feature that isn't supported

                    if (match.Groups["tag"].Success) compound.Tag = match.Groups["tag"].Value;
                    if (match.Groups["class"].Success) compound.Classes.Add(match.Groups["class"].Value);
                    if (match.Groups["id"].Success) compound.Id = match.Groups["id"].Value;
                    pos += match.Length;
                }
                result.Add(compound);
            }

            return result.Count > 0 ? result : null;
        }
    }
}
