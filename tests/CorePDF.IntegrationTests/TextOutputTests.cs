using CorePDF.Contents;
using CorePDF.Embeds;
using CorePDF.Pages;
using CorePDF.TypeFaces;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace CorePDF.IntegrationTests
{
    /// <summary>
    /// Checks that the text written to the document can be read back as the same Unicode text
    /// </summary>
    [Trait("Category", "Integration")]
    public class TextOutputTests
    {
        private const string FONTFILE = "GreatVibes-Regular.ttf";

        internal static PdfInspector Publish(Document document)
        {
            using (var stream = new MemoryStream())
            {
                document.Publish(stream);
                return new PdfInspector(stream.ToArray());
            }
        }

        internal static Document SinglePage(params Content[] contents)
        {
            var document = new Document();
            document.Pages.Add(new Page { Contents = contents.ToList() });
            return document;
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void StandardFont_WinAnsiText_ReadsBackTheSame(bool compress)
        {
            const string text = "Café – “quotes” cost €5 ± ½ (x) \\ ©";
            var document = SinglePage(new TextBox { Text = text, PosX = 40, PosY = 700 });
            document.CompressContent = compress;

            var pdf = Publish(document);

            Assert.Equal(new[] { text }, pdf.PageText(pdf.Pages[0]));
            Assert.Contains("/Encoding /WinAnsiEncoding", Assert.Single(pdf.Fonts).Dictionary);
        }

        [Fact]
        public void StandardFont_CharacterOutsideWinAnsi_ThrowsHelpfulError()
        {
            var document = SinglePage(new TextBox { Text = "Ωmega", PosX = 40, PosY = 700 });

            var ex = Assert.Throws<ArgumentException>(() => Publish(document));
            Assert.Contains("U+03A9", ex.Message);
        }

        [Fact]
        public void OnlyTheFontsThatAreUsed_AreIncluded()
        {
            var pdf = Publish(SinglePage(
                new TextBox { Text = "One", FontFace = Fonts.FONTSANSSERIF, PosX = 40, PosY = 700 },
                new TextBox { Text = "Two", FontFace = Fonts.FONTSERIFBOLD, PosX = 40, PosY = 680 },
                new Contents.Barcode { Text = "123", PosX = 40, PosY = 600 }));

            var names = pdf.Fonts.Select(f => Regex.Match(f.Dictionary, @"/BaseFont /(\S+)").Groups[1].Value).OrderBy(n => n);
            Assert.Equal(new[] { "Helvetica", "Times-Bold" }, names);
        }

        [Fact]
        public void EmbeddedFont_IsWrittenAsSubsetType0FontWithToUnicode()
        {
            const string text = "Zażółć “ﬁne” €5";
            var document = SinglePage(new TextBox { Text = text, FontFace = "GV", PosX = 40, PosY = 700 });
            document.EmbedFont(new FontFile { Name = "GV", FilePath = FONTFILE });

            var pdf = Publish(document);

            var type0 = Assert.Single(pdf.Fonts);
            Assert.Contains("/Subtype /Type0", type0.Dictionary);
            Assert.Contains("/Encoding /Identity-H", type0.Dictionary);
            Assert.Matches(@"/BaseFont /[A-Z]{6}\+GreatVibes-Regular\n", type0.Dictionary);

            var cidFont = pdf.Resolve(Regex.Match(type0.Dictionary, @"/DescendantFonts \[(\d+ 0 R)\]").Groups[1].Value);
            Assert.Contains("/Subtype /CIDFontType2", cidFont.Dictionary);
            Assert.Contains("/CIDToGIDMap /Identity", cidFont.Dictionary);

            var descriptor = pdf.Resolve(Regex.Match(cidFont.Dictionary, @"/FontDescriptor (\d+ 0 R)").Groups[1].Value);
            var fontFile = pdf.Resolve(Regex.Match(descriptor.Dictionary, @"/FontFile2 (\d+ 0 R)").Groups[1].Value);
            var length1 = int.Parse(Regex.Match(fontFile.Dictionary, @"/Length1 (\d+)").Groups[1].Value);
            Assert.Equal(fontFile.Stream.Length, length1);
            Assert.True(length1 < new FileInfo(FONTFILE).Length / 2, "the font program should be subset");

            Assert.Equal(new[] { text }, pdf.PageText(pdf.Pages[0]));
        }

        [Fact]
        public void EmbeddedFont_MetricsComeFromTheFontFile()
        {
            var document = SinglePage(new TextBox { Text = "Hi", FontFace = "GV", PosX = 40, PosY = 700 });
            document.EmbedFont(new FontFile { Name = "GV", FilePath = FONTFILE });

            var pdf = Publish(document);
            var descriptor = pdf.Objects.Values.Single(o => o.Dictionary.Contains("/Type /FontDescriptor"));

            Assert.Matches(@"/Ascent [1-9]\d*\n", descriptor.Dictionary);
            Assert.Matches(@"/Descent -[1-9]\d*\n", descriptor.Dictionary);
            Assert.Matches(@"/FontBBox \[-?\d+ -\d+ \d+ \d+\]", descriptor.Dictionary);
            Assert.Matches(@"/CapHeight [1-9]\d*\n", descriptor.Dictionary);
        }

        [Fact]
        public void EmbeddedFont_SubsetTagDependsOnTheCharactersUsed()
        {
            string Tag(string text)
            {
                var document = SinglePage(new TextBox { Text = text, FontFace = "GV", PosX = 40, PosY = 700 });
                document.EmbedFont(new FontFile { Name = "GV", FilePath = FONTFILE });
                return Regex.Match(Assert.Single(Publish(document).Fonts).Dictionary, @"/BaseFont /([A-Z]{6})\+").Groups[1].Value;
            }

            Assert.Equal(Tag("abc"), Tag("cab"));
            Assert.NotEqual(Tag("abc"), Tag("xyz"));
        }

        [Fact]
        public void EmbeddedFont_CharacterNotInFont_ThrowsHelpfulError()
        {
            var document = SinglePage(new TextBox { Text = "Кириллица", FontFace = "GV", PosX = 40, PosY = 700 });
            document.EmbedFont(new FontFile { Name = "GV", FilePath = FONTFILE });

            var ex = Assert.Throws<ArgumentException>(() => Publish(document));
            Assert.Contains("U+041A", ex.Message);
            Assert.Contains("GV", ex.Message);
        }

        [Fact]
        public void EmbeddedFont_FromFileData_IsTheSameAsFromFilePath()
        {
            Document Create(FontFile font)
            {
                var document = SinglePage(new TextBox { Text = "Same", FontFace = "GV", PosX = 40, PosY = 700 });
                document.EmbedFont(font);
                return document;
            }

            var fromPath = Publish(Create(new FontFile { Name = "GV", FilePath = FONTFILE })).Data;
            var fromData = Publish(Create(new FontFile { Name = "GV", FileData = File.ReadAllBytes(FONTFILE) })).Data;

            Assert.Equal(fromPath, fromData);
        }

        [Fact]
        public void EmbeddedFont_BoldTagsUseTheBoldVariantOfTheFamily()
        {
            var document = SinglePage(new TextBox { Text = "plain <b>bold</b>", FontFace = "GV", PosX = 40, PosY = 700 });
            document.EmbedFont(new FontFile { Name = "GV", BaseFont = "Vibes", FilePath = FONTFILE });
            document.EmbedFont(new FontFile { Name = "GV-Bold", BaseFont = "Vibes", Bold = true, FilePath = FONTFILE });

            var pdf = Publish(document);

            Assert.Equal(2, pdf.Fonts.Count);
            Assert.Equal(new[] { "plain bold" }, pdf.PageText(pdf.Pages[0]));
        }

        [Fact]
        public void EmbeddedFont_MissingFile_ThrowsFileNotFound()
        {
            var document = SinglePage(new TextBox { Text = "x", PosX = 40, PosY = 700 });
            document.EmbedFont(new FontFile { Name = "Missing", FilePath = "does-not-exist.ttf" });

            Assert.Throws<FileNotFoundException>(() => Publish(document));
        }

        [Fact]
        public void ObsoleteFontMetrics_AreAcceptedAndIgnored()
        {
            Document Create(FontFile font)
            {
                var document = SinglePage(new TextBox { Text = "Compat", FontFace = "GV", PosX = 40, PosY = 700 });
                document.EmbedFont(font);
                return document;
            }

#pragma warning disable CS0618 // checking the obsolete properties still compile and have no effect
            var legacy = new FontFile
            {
                Name = "GV",
                FilePath = FONTFILE,
                Ascent = 1,
                Descent = -1,
                MaximumWidth = 2,
                StemV = 3,
                Widths = new List<int> { 1, 2, 3 },
                Type = FontType.TrueType
            };
#pragma warning restore CS0618

            Assert.Equal(Publish(Create(new FontFile { Name = "GV", FilePath = FONTFILE })).Data, Publish(Create(legacy)).Data);
        }

        [Fact]
        public void WrappedMixedStyleText_ReadsBackWithoutLosingWords()
        {
            var pdf = Publish(SinglePage(new TextBox
            {
                Text = "Some <b>bold words</b> and <i>italic words</i> wrapped\nonto lines",
                PosX = 40,
                PosY = 700,
                Width = 80
            }));

            var lines = pdf.PageText(pdf.Pages[0]);
            Assert.True(lines.Count > 3);
            Assert.Equal("Some bold words and italic words wrapped onto lines", string.Join(" ", lines));
        }

        [Fact]
        public void BarcodeText_IsEscaped()
        {
            var pdf = Publish(SinglePage(new Contents.Barcode { Text = "AB(1)", FontFace = Fonts.BARCODE128, ShowText = true, PosX = 40, PosY = 600 }));

            Assert.Contains("AB(1)", pdf.PageText(pdf.Pages[0]));
        }

        [Fact]
        public void DocumentInformation_SupportsUnicodeAndDelimiters()
        {
            var document = SinglePage();
            document.Properties.Title = "Café (draft)";
            document.Properties.Author = "Name (with) brackets";

            var info = Publish(document).Info.Dictionary;

            // "Café (draft)" as UTF-16BE with a byte order mark
            Assert.Contains("/Title <FEFF00430061006600E900200028006400720061006600740029>", info);
            Assert.Contains("/Author (Name \\(with\\) brackets)", info);
            Assert.Contains("/Producer (CorePDF)", info);
        }
    }
}
