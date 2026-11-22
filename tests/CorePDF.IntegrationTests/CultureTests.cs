using CorePDF.Contents;
using CorePDF.Embeds;
using CorePDF.Pages;
using CorePDF.TypeFaces;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Xunit;

namespace CorePDF.IntegrationTests
{
    /// <summary>
    /// PDF syntax always uses '.' as the decimal separator and '-' as the minus sign, so the
    /// output must not depend on the culture of the machine that produces it.
    /// </summary>
    [Trait("Category", "Integration")]
    public class CultureTests
    {
        [Theory]
        [InlineData("de-DE")] // decimal comma
        [InlineData("fr-FR")] // decimal comma, non-breaking space group separator
        [InlineData("sv-SE")] // decimal comma, U+2212 minus sign
        [InlineData("th-TH")] // Buddhist calendar
        [InlineData("ar-SA")] // Um Al-Qura calendar, right-to-left marks
        public void Publish_InDifferentCulture_ProducesSameOutputAsInvariant(string cultureName)
        {
            var expected = PublishIn(CultureInfo.InvariantCulture);
            var actual = PublishIn(CultureInfo.GetCultureInfo(cultureName));

            if (!expected.SequenceEqual(actual))
            {
                // report the first lines that differ so a failure points at the culprit
                var expectedLines = Encoding.Latin1.GetString(expected).Split('\n');
                var actualLines = Encoding.Latin1.GetString(actual).Split('\n');
                var differences = expectedLines.Zip(actualLines)
                    .Where(pair => pair.First != pair.Second)
                    .Take(5)
                    .Select(pair => $"  expected: {pair.First}\n  actual:   {pair.Second}");

                Assert.Fail($"Output in {cultureName} differs from the invariant culture:\n{string.Join("\n", differences)}");
            }
        }

        [Fact]
        public void Publish_UtcDate_WrittenWith24HourTimeAndZeroOffset()
        {
            var document = new Document();
            document.Properties.CreationDate = new DateTime(2026, 3, 4, 17, 6, 7, DateTimeKind.Utc);

            Assert.Contains("/CreationDate (D:20260304170607+00'00')", PublishToString(document));
        }

        [Fact]
        public void Publish_LocalDate_WrittenWithLocalOffsetForThatDate()
        {
            var date = new DateTime(2026, 7, 1, 9, 30, 0, DateTimeKind.Local);
            var offset = TimeZoneInfo.Local.GetUtcOffset(date);
            var expected = string.Format(CultureInfo.InvariantCulture, "/CreationDate (D:20260701093000{0}{1:00}'{2:00}')",
                offset < TimeSpan.Zero ? '-' : '+', Math.Abs(offset.Hours), Math.Abs(offset.Minutes));

            var document = new Document();
            document.Properties.CreationDate = date;

            Assert.Contains(expected, PublishToString(document));
        }

        private static string PublishToString(Document document)
        {
            using (var stream = new MemoryStream())
            {
                document.Publish(stream);
                return Encoding.Latin1.GetString(stream.ToArray());
            }
        }

        private static byte[] PublishIn(CultureInfo culture)
        {
            var original = CultureInfo.CurrentCulture;
            var originalUI = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentCulture = culture;
                CultureInfo.CurrentUICulture = culture;

                using (var stream = new MemoryStream())
                {
                    BuildDocument().Publish(stream);
                    return stream.ToArray();
                }
            }
            finally
            {
                CultureInfo.CurrentCulture = original;
                CultureInfo.CurrentUICulture = originalUI;
            }
        }

        private static Document BuildDocument()
        {
            var document = new Document();
            document.Properties.Title = "Culture test";
            document.Properties.Author = "CorePDF";
            document.Properties.CreationDate = new DateTime(2026, 3, 4, 5, 6, 7);
            document.Properties.ModifiedDate = new DateTime(2026, 8, 9, 10, 11, 12);

            document.EmbedFont(new FontFile
            {
                Name = "GreatVibes-Regular",
                BaseFont = "GreatVibes",
                FilePath = "GreatVibes-Regular.ttf"
            });

            document.Images = new List<ImageFile>
            {
                new ImageFile { Name = "vector", FilePath = "sample.svg" },
                new ImageFile { Name = "photo", FilePath = "smiley.jpg" },
                new ImageFile { Name = "transparent", FilePath = "sample.png" }
            };

            document.HeadersFooters = new List<HeaderFooter>
            {
                new HeaderFooter
                {
                    Name = "header",
                    Contents = new List<Content>
                    {
                        new TextBox { Text = "Header text", PosX = 40, PosY = 810, FontSize = 9, Color = "#336699" }
                    }
                }
            };

            var table = new Table
            {
                Width = 333,
                PosX = 40,
                PosY = 250,
                Border = new BorderPattern { Top = new Stroke { Width = 0.75m, Color = "#808080" } }
            };
            var row = table.AddRow();
            var cell1 = row.AddColumn();
            cell1.Width = 33.3m;
            cell1.TextContent = new TextBox { Text = "Cell one", FontFace = Fonts.FONTSANSSERIFBOLD, FontSize = 11 };
            var cell2 = row.AddColumn();
            cell2.Width = 66.7m;
            cell2.TextContent = new TextBox { Text = "Cell two", FontSize = 11, TextAlignment = Alignment.Right };

            document.Pages.Add(new Page
            {
                PageSize = Paper.PAGEA4PORTRAIT,
                HeaderName = "header",
                Contents = new List<Content>
                {
                    new Image { ImageName = "vector", PosX = 20, PosY = 600, ScaleFactor = 0.37m },
                    new Image { ImageName = "photo", PosX = 200, PosY = 600, ScaleFactor = 0.23m },
                    new Image { ImageName = "transparent", PosX = 400, PosY = 600, ScaleFactor = 1.5m },
                    new TextBox { Text = "Centred text", FontSize = 23, PosX = 300, PosY = 500, TextAlignment = Alignment.Center, Color = "#ab1234" },
                    new TextBox { Text = "Embedded font, wrapped across lines in a narrow box", FontFace = "GreatVibes-Regular", FontSize = 17, PosX = 40, PosY = 450, Width = 150 },
                    new Contents.Barcode { Text = "CorePDF", PosX = 40, PosY = 150 },
                    new Shape { Type = Polygon.Rectangle, PosX = 300, PosY = 300, Width = 120, Height = 80, BorderRadius = 7, FillColor = "#c0ffee", Stroke = new Stroke { Width = 1.25m, Color = "teal" } },
                    new Shape { Type = Polygon.Ellipses, PosX = 450, PosY = 350, Width = 90, Height = 60, Stroke = new Stroke { Width = 0.5m, Color = "#f00" } },
                    new Shape { Type = Polygon.Line, PosX = 40, PosY = 100, Width = 500, Height = -20, Stroke = new Stroke { Width = 2.5m } },
                    table
                }
            });

            return document;
        }
    }
}
