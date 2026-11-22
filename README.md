# CorePDF
[![CI](https://github.com/GZidar/CorePDF/actions/workflows/ci.yml/badge.svg?branch=development)](https://github.com/GZidar/CorePDF/actions/workflows/ci.yml)

A small, MIT licensed PDF generation library for .NET that you can do with as you please.

Supports .NET 8 and later, and has no dependencies on other packages.

## Features
- Text with automatic wrapping, alignment and inline `<b>` / `<i>` styling
- The 14 standard PDF fonts, using the Windows (WinAnsi) character set
- Embedded TrueType fonts with full Unicode support (accented letters, Greek, Cyrillic,
  CJK, etc). Font metrics are read from the font file and only the characters actually
  used are embedded, which keeps documents small
- Images (JPEG, PNG, GIF and BMP, plus SVG drawn as vector paths), shapes, tables, barcodes,
  headers and footers. JPEG files are embedded as they are, without being re-encoded
- Hyperlinks on any content
- PDF/A-2b and PDF/A-2u archival documents
- Deterministic output: the same document always produces exactly the same bytes

## Important
This library does not (yet) support the creation of encrypted or secure PDFs. So it should not be used
to store sensitive information.

## Example
``` csharp
var document = new Document();

// Add any images that you want to include in the document
document.Images.Add(new ImageFile
{
    Name = "smiley",
    FilePath = "smiley.jpg"
});

// Embed any TrueType fonts you want to use. The font's metrics are read from the file.
document.EmbedFont(new FontFile
{
    Name = "GreatVibes-Regular",
    FilePath = "GreatVibes-Regular.ttf"
});

// Add the content for each page
document.Pages.Add(new Page
{
    PageSize = Paper.PAGEA4PORTRAIT,
    Contents = new List<Content>()
    {
        new Image
        {
            ImageName = "smiley",
            PosX = 200,
            PosY = 600,
            ScaleFactor = 0.4m
        },
        new TextBox
        {
            Text = "This is a test document",
            FontFace = "GreatVibes-Regular",
            FontSize = 30,
            PosX = 250,
            PosY = 400,
            TextAlignment = Alignment.Center
        },
        new TextBox
        {
            Text = "Visit <b>example.com</b>",
            FontFace = Fonts.FONTSANSSERIF,
            FontSize = 12,
            PosX = 250,
            PosY = 350,
            TextAlignment = Alignment.Center,
            Hyperlink = "https://example.com"
        },
        new Shape
        {
            Type = Polygon.Rectangle,
            PosX = 200,
            PosY = 200,
            Height = 300,
            Width = 300,
            FillColor = "#ffffff",
            ZIndex = 0
        },
        new Shape
        {
            Type = Polygon.Ellipses,
            PosX = 350,
            PosY = 350,
            Stroke = new Stroke
            {
                Color = "#ff0000"
            },
            Height = 500,
            Width = 300,
            ZIndex = 10
        }
    }
});

// Make the file
using (var filestream = new FileStream("sample.pdf", FileMode.Create, FileAccess.Write))
{
    document.Publish(filestream);
}
```

`Publish` works with any writable stream (including ones that can't seek, like an HTTP
response body) and leaves the stream open.

## Fonts and characters
The standard fonts (`Fonts.FONTSANSSERIF`, `Fonts.FONTSERIF`, `Fonts.FONTFIXED` and their bold
and italic variants) are built into every PDF reader, so they don't add anything to the size
of the document. They are limited to the Windows-1252 character set, which covers English and
most Western European languages.

For anything else, embed a TrueType (`.ttf` or `.ttc`) font with `EmbedFont` and use its name
as the `FontFace`. To use `<b>` and `<i>` with an embedded font, embed the bold and italic
versions too and give them the same `BaseFont`:

``` csharp
document.EmbedFont(new FontFile { Name = "NotoSans", BaseFont = "NotoSans", FilePath = "NotoSans-Regular.ttf" });
document.EmbedFont(new FontFile { Name = "NotoSans-Bold", BaseFont = "NotoSans", Bold = true, FilePath = "NotoSans-Bold.ttf" });
```

If text contains a character the chosen font can't show, `Publish` throws an exception that
names the character, rather than producing a document with missing or garbled text.

OpenType fonts with PostScript (CFF) outlines (usually `.otf` files) are not supported yet.

## PDF/A
Set `Conformance` to produce an archival PDF/A document:

``` csharp
var document = new Document { Conformance = PdfConformance.PdfA2B };
```

PDF/A documents must embed every font they use, so all text must use embedded fonts rather
than the standard fonts. `Publish` explains what needs to change if a document can't comply.
`PdfConformance.PdfA2U` additionally guarantees that all text can be extracted as Unicode.

# License
Copyright (c) 2024 CorePDF Contributors

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
