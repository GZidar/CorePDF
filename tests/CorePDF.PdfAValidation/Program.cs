using CorePDF;
using CorePDF.Contents;
using CorePDF.Embeds;
using CorePDF.Pages;
using CorePDF.TypeFaces;
using System;
using System.Collections.Generic;
using System.IO;

// Writes a set of PDF/A documents covering the library's features, so they can be checked
// with an independent PDF/A validator (veraPDF in CI).
//
// usage: dotnet run --project tests/CorePDF.PdfAValidation -- <output folder>

var outputFolder = args.Length > 0 ? args[0] : "pdfa-output";
Directory.CreateDirectory(outputFolder);

var assets = Path.Combine(AppContext.BaseDirectory, "assets");
string Asset(string name) => Path.Combine(assets, name);

void Save(Document document, string name)
{
    var path = Path.Combine(outputFolder, name);
    using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
    {
        document.Publish(stream);
    }
    Console.WriteLine($"wrote {path}");
}

Document NewDocument(PdfConformance conformance, string title, bool compress)
{
    var document = new Document { Conformance = conformance, CompressContent = compress };
    document.Properties.Title = title;
    document.Properties.Author = "CorePDF – tests";
    document.Properties.Subject = "PDF/A validation (with brackets)";
    document.Properties.Keywords = "pdf/a, validation";
    document.Properties.CreationDate = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
    document.Properties.ModifiedDate = new DateTime(2026, 6, 7, 8, 9, 10, DateTimeKind.Local);

    // the same font file stands in for the bold variant of the family
    document.EmbedFont(new FontFile { Name = "Vibes", BaseFont = "Vibes", FilePath = Asset("GreatVibes-Regular.ttf") });
    document.EmbedFont(new FontFile { Name = "Vibes-Bold", BaseFont = "Vibes", Bold = true, FilePath = Asset("GreatVibes-Regular.ttf") });
    return document;
}

foreach (var conformance in new[] { PdfConformance.PdfA2B, PdfConformance.PdfA2U })
{
    var level = conformance == PdfConformance.PdfA2B ? "2b" : "2u";

    // text, links, tables, shapes, headers and footers over several pages
    foreach (var compress in new[] { false, true })
    {
        var document = NewDocument(conformance, $"Text – PDF/A-{level}", compress);
        document.HeadersFooters.Add(new HeaderFooter
        {
            Name = "footer",
            Contents = new List<Content>
            {
                new TextBox { Text = "Footer with a link", FontFace = "Vibes", FontSize = 12, PosX = 300, PosY = 30, TextAlignment = Alignment.Center, Hyperlink = "https://example.com/footer" },
                new Shape { Type = Polygon.Line, PosX = 40, PosY = 50, Width = 515, Height = 0 }
            }
        });

        for (var pageNumber = 1; pageNumber <= 2; pageNumber++)
        {
            var table = new Table { PosX = 40, PosY = 500, Width = 400, Border = new BorderPattern { Top = new Stroke(), Bottom = new Stroke(), Left = new Stroke(), Right = new Stroke() } };
            var row = table.AddRow();
            var left = row.AddColumn();
            left.Width = 50;
            left.TextContent = new TextBox { Text = "Cell <b>one</b>", FontFace = "Vibes", FontSize = 14 };
            var right = row.AddColumn();
            right.Width = 50;
            right.TextContent = new TextBox { Text = "Cell two links", FontFace = "Vibes", FontSize = 14, Hyperlink = "mailto:someone@example.com" };

            document.Pages.Add(new Page
            {
                FooterName = "footer",
                Contents = new List<Content>
                {
                    new TextBox { Text = $"Page {pageNumber}: Zażółć gęślą jaźń “quotes” – ﬁne €5", FontFace = "Vibes", FontSize = 20, PosX = 40, PosY = 780 },
                    new TextBox { Text = "Wrapped text with <b>bold</b> runs that goes on long enough to need several lines in a narrow box.", FontFace = "Vibes", FontSize = 14, PosX = 40, PosY = 740, Width = 250 },
                    new TextBox { Text = "Right aligned link", FontFace = "Vibes", FontSize = 16, PosX = 555, PosY = 700, TextAlignment = Alignment.Right, Hyperlink = "https://example.com/ünïcödé?a=1&b=(2)" },
                    new Shape { Type = Polygon.Rectangle, PosX = 300, PosY = 300, Width = 200, Height = 100, BorderRadius = 10, FillColor = "#eeeeff", Stroke = new Stroke { Color = "#3366cc", Width = 2, DashPattern = "3 2" } },
                    new Shape { Type = Polygon.Ellipses, PosX = 150, PosY = 250, Width = 120, Height = 60, FillColor = "#ffcc00", Hyperlink = "https://example.com/ellipse" },
                    new CorePDF.Contents.Barcode { Text = "CorePDF", FontFace = Fonts.BARCODE128, ShowText = true, TextFont = "Vibes", FontSize = 12, PosX = 300, PosY = 150 },
                    table
                }
            });
        }

        Save(document, $"pdfa-{level}-text{(compress ? "-compressed" : "")}.pdf");
    }

    // every supported image type, including transparency
    var images = NewDocument(conformance, $"Images – PDF/A-{level}", compress: true);
    foreach (var file in new[] { "sample.png", "rpng2-bg16-toucan.png", "square.gif", "smiley.jpg", "sample.svg", "opacity.svg", "clip-path.svg", "css-styles.svg", "use-defs.svg" })
    {
        images.Images.Add(new ImageFile { Name = file, FilePath = Asset(file) });
    }
    images.Pages.Add(new Page
    {
        Contents = new List<Content>
        {
            new TextBox { Text = "Images", FontFace = "Vibes", FontSize = 24, PosX = 40, PosY = 790 },
            new Shape { Type = Polygon.Rectangle, PosX = 30, PosY = 640, Width = 200, Height = 60, FillColor = "#3366cc" },
            new Image { ImageName = "sample.png", PosX = 40, PosY = 630, Hyperlink = "https://example.com/image" },
            new Image { ImageName = "smiley.jpg", PosX = 300, PosY = 600, ScaleFactor = 0.4m },
            new Image { ImageName = "square.gif", PosX = 450, PosY = 450, Width = 60, Height = 60 },
            new Image { ImageName = "rpng2-bg16-toucan.png", PosX = 40, PosY = 380 },
            new Image { ImageName = "sample.svg", PosX = 260, PosY = 100, ScaleFactor = 0.5m },
            // SVG images use transparency groups (opacity), clipping and reusable drawings
            new Image { ImageName = "opacity.svg", PosX = 40, PosY = 150, ScaleFactor = 0.5m },
            new Image { ImageName = "clip-path.svg", PosX = 160, PosY = 150, ScaleFactor = 0.5m },
            new Image { ImageName = "css-styles.svg", PosX = 300, PosY = 380, ScaleFactor = 0.5m },
            new Image { ImageName = "use-defs.svg", PosX = 420, PosY = 380, ScaleFactor = 0.5m, Hyperlink = "https://example.com/svg" }
        }
    });
    Save(images, $"pdfa-{level}-images.pdf");

    // a page with no text at all, so no fonts
    var shapesOnly = new Document { Conformance = conformance };
    shapesOnly.Pages.Add(new Page { Contents = new List<Content> { new Shape { Type = Polygon.Rectangle, PosX = 100, PosY = 100, Width = 50, Height = 50 } } });
    Save(shapesOnly, $"pdfa-{level}-no-text.pdf");
}
