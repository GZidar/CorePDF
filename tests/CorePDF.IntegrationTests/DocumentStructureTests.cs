using CorePDF.Contents;
using CorePDF.Embeds;
using CorePDF.Pages;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Xunit;
using static CorePDF.IntegrationTests.TextOutputTests;

namespace CorePDF.IntegrationTests
{
    /// <summary>
    /// Checks the overall structure of published documents: cross references, links, PDF/A
    /// requirements and the handling of the output stream.
    /// </summary>
    [Trait("Category", "Integration")]
    public class DocumentStructureTests
    {
        private static Document ComplexDocument(PdfConformance conformance = PdfConformance.None)
        {
            var document = new Document { Conformance = conformance, CompressContent = true };
            document.Properties.Title = "Structure – test";
            document.Properties.Author = "Tester";
            document.Properties.Subject = "Subject";
            document.Properties.Keywords = "a, b";
            document.Properties.CreationDate = new DateTime(2026, 1, 2, 3, 4, 5, DateTimeKind.Utc);
            document.Properties.ModifiedDate = new DateTime(2026, 2, 3, 4, 5, 6, DateTimeKind.Utc);
            document.EmbedFont(new FontFile { Name = "GV", FilePath = "GreatVibes-Regular.ttf" });
            document.Images.Add(new ImageFile { Name = "toucan", FilePath = "rpng2-bg16-toucan.png" });
            document.Images.Add(new ImageFile { Name = "svg", FilePath = "sample.svg" });
            document.HeadersFooters.Add(new HeaderFooter
            {
                Name = "header",
                Contents = new List<Content> { new TextBox { Text = "Header link", FontFace = "GV", PosX = 40, PosY = 820, Hyperlink = "https://example.com/header" } }
            });

            for (var i = 0; i < 2; i++)
            {
                var table = new Table { PosX = 40, PosY = 500, Width = 300 };
                var cell = table.AddRow().AddColumn();
                cell.Width = 100;
                cell.TextContent = new TextBox { Text = "Cell link", FontFace = "GV", Hyperlink = "https://example.com/cell" };

                document.Pages.Add(new Page
                {
                    HeaderName = "header",
                    Contents = new List<Content>
                    {
                        new TextBox { Text = "Page text żółć", FontFace = "GV", PosX = 40, PosY = 700, Hyperlink = "https://example.com/ünï" },
                        new Image { ImageName = "toucan", PosX = 40, PosY = 550, ScaleFactor = 0.25m, Hyperlink = "mailto:someone@example.com" },
                        new Image { ImageName = "toucan", PosX = 100, PosY = 550, ScaleFactor = 0.25m },
                        new Image { ImageName = "svg", PosX = 200, PosY = 550 },
                        new Shape { Type = Polygon.Ellipses, PosX = 300, PosY = 300, Width = 40, Height = 20, Hyperlink = "https://example.com/shape" },
                        new TextBox { Text = "" },
                        table
                    }
                });
            }

            return document;
        }

        [Theory]
        [InlineData(PdfConformance.None)]
        [InlineData(PdfConformance.PdfA2B)]
        public void ComplexDocument_EveryObjectIsReachableThroughTheCrossReferenceTable(PdfConformance conformance)
        {
            // the inspector fails if any cross reference entry is malformed or points at the wrong place
            var pdf = Publish(ComplexDocument(conformance));

            Assert.Equal(2, pdf.Pages.Count);
            Assert.Matches(@"/Size \d+\n/Root \d+ 0 R\n/Info \d+ 0 R\n/ID \[<[0-9A-F]{32}> <[0-9A-F]{32}>\]", pdf.Trailer);

            // every object reference in the document points at an object that exists
            foreach (var obj in pdf.Objects.Values)
            {
                foreach (Match reference in Regex.Matches(obj.Dictionary, @"(\d+) 0 R"))
                {
                    Assert.True(pdf.Objects.ContainsKey(int.Parse(reference.Groups[1].Value)), $"object {obj.Number} refers to missing object {reference.Value}");
                }
            }
        }

        [Fact]
        public void ComplexDocument_PublishingTwice_GivesIdenticalOutput()
        {
            var document = ComplexDocument(PdfConformance.PdfA2U);

            Assert.Equal(Publish(document).Data, Publish(document).Data);
        }

        [Fact]
        public void Links_AreWrittenAsUriAnnotationsOnEachPage()
        {
            var pdf = Publish(ComplexDocument());

            foreach (var page in pdf.Pages)
            {
                var annots = Regex.Matches(Regex.Match(page.Dictionary, @"/Annots \[([^\]]*)\]").Groups[1].Value, @"\d+ 0 R")
                    .Select(m => pdf.Resolve(m.Value))
                    .ToList();

                var uris = annots.Select(a => Regex.Match(a.Dictionary, @"/URI \(([^)]*)\)").Groups[1].Value).OrderBy(u => u).ToList();
                Assert.Equal(new[]
                {
                    "https://example.com/%C3%BCn%C3%AF",
                    "https://example.com/cell",
                    "https://example.com/header",
                    "https://example.com/shape",
                    "mailto:someone@example.com"
                }, uris);

                foreach (var annot in annots)
                {
                    Assert.Contains("/Subtype /Link", annot.Dictionary);
                    Assert.Contains("/F 4", annot.Dictionary);
                    Assert.Matches(@"/Rect \[[\d.]+ [\d.]+ [\d.]+ [\d.]+\]", annot.Dictionary);
                }
            }
        }

        [Fact]
        public void SvgImages_AreDrawnAsPathsNotEmbeddedFiles()
        {
            var pdf = Publish(ComplexDocument());
            var text = Encoding.Latin1.GetString(pdf.Data);

            Assert.DoesNotContain("/EmbeddedFile", text);
            Assert.Single(pdf.Objects.Values, o => o.Dictionary.Contains("/Subtype /Image") && o.Dictionary.Contains("/DeviceRGB"));
        }

        [Fact]
        public void EmptyContent_IsNotReferencedAsAContentStream()
        {
            var pdf = Publish(ComplexDocument());

            foreach (var page in pdf.Pages)
            {
                foreach (Match reference in Regex.Matches(Regex.Match(page.Dictionary, @"/Contents \[([^\]]*)\]").Groups[1].Value, @"\d+ 0 R"))
                {
                    Assert.NotNull(pdf.Resolve(reference.Value).Stream);
                }
            }
        }

        [Fact]
        public void PdfA_IncludesMetadataAndOutputIntent()
        {
            var pdf = Publish(ComplexDocument(PdfConformance.PdfA2B));
            var catalog = pdf.Catalog.Dictionary;

            Assert.Equal("%PDF-1.7", pdf.Header);

            var metadata = pdf.Resolve(Regex.Match(catalog, @"/Metadata (\d+ 0 R)").Groups[1].Value);
            Assert.DoesNotContain("/Filter", metadata.Dictionary);
            var xmp = Encoding.UTF8.GetString(metadata.Stream);
            Assert.Contains("<pdfaid:part>2</pdfaid:part>", xmp);
            Assert.Contains("<pdfaid:conformance>B</pdfaid:conformance>", xmp);
            Assert.DoesNotContain("encoding=", xmp);

            Assert.Contains("/S /GTS_PDFA1", catalog);
            var profile = pdf.Resolve(Regex.Match(catalog, @"/DestOutputProfile (\d+ 0 R)").Groups[1].Value);
            Assert.Contains("/N 3", profile.Dictionary);
            Assert.Equal("acsp", Encoding.ASCII.GetString(profile.Stream, 36, 4));
        }

        [Fact]
        public void PdfA_MetadataMatchesTheDocumentInformation()
        {
            var pdf = Publish(ComplexDocument(PdfConformance.PdfA2U));
            var info = pdf.Info.Dictionary;
            var xmp = Encoding.UTF8.GetString(pdf.Resolve(Regex.Match(pdf.Catalog.Dictionary, @"/Metadata (\d+ 0 R)").Groups[1].Value).Stream);

            Assert.Contains("<pdfaid:conformance>U</pdfaid:conformance>", xmp);
            Assert.Contains(">Structure – test</rdf:li>", xmp);
            Assert.Contains("<rdf:li>Tester</rdf:li>", xmp);
            Assert.Contains(">Subject</rdf:li>", xmp);
            Assert.Contains("<pdf:Keywords>a, b</pdf:Keywords>", xmp);
            Assert.Contains("<pdf:Producer>CorePDF</pdf:Producer>", xmp);
            Assert.Contains("<xmp:CreatorTool>CorePDF</xmp:CreatorTool>", xmp);
            Assert.Contains("<xmp:CreateDate>2026-01-02T03:04:05+00:00</xmp:CreateDate>", xmp);
            Assert.Contains("/CreationDate (D:20260102030405+00'00')", info);
            Assert.Contains("<xmp:ModifyDate>2026-02-03T04:05:06+00:00</xmp:ModifyDate>", xmp);
            Assert.Contains("/ModDate (D:20260203040506+00'00')", info);
        }

        [Fact]
        public void PdfA_ImagesAreNotInterpolated()
        {
            var standard = Encoding.Latin1.GetString(Publish(ComplexDocument()).Data);
            var archival = Encoding.Latin1.GetString(Publish(ComplexDocument(PdfConformance.PdfA2B)).Data);

            Assert.Contains("/Interpolate", standard);
            Assert.DoesNotContain("/Interpolate", archival);
        }

        [Theory]
        [InlineData(PdfConformance.PdfA2B)]
        [InlineData(PdfConformance.PdfA2U)]
        public void PdfA_WithAStandardFont_ThrowsHelpfulError(PdfConformance conformance)
        {
            var document = SinglePage(new TextBox { Text = "Helvetica text", PosX = 40, PosY = 700 });
            document.Conformance = conformance;

            var ex = Assert.Throws<InvalidOperationException>(() => Publish(document));
            Assert.Contains("'Helvetica'", ex.Message);
            Assert.Contains("EmbedFont", ex.Message);
        }

        [Fact]
        public void Publish_ToANonSeekableStream_Works()
        {
            var document = ComplexDocument();
            using var target = new MemoryStream();
            using var nonSeekable = new WriteOnlyStream(target);

            document.Publish(nonSeekable);

            Assert.Equal(Publish(document).Data, target.ToArray());
        }

        [Fact]
        public void SameContentOnTwoPages_ThrowsHelpfulError()
        {
            var shared = new TextBox { Text = "Shared", PosX = 40, PosY = 700 };
            var document = new Document();
            document.Pages.Add(new Page { Contents = new List<Content> { shared } });
            document.Pages.Add(new Page { Contents = new List<Content> { shared } });

            var ex = Assert.Throws<InvalidOperationException>(() => Publish(document));
            Assert.Contains("header or footer", ex.Message);
        }

        [Fact]
        public void Publish_LeavesTheStreamOpen()
        {
            using var stream = new MemoryStream();

            SinglePage(new TextBox { Text = "x", PosX = 40, PosY = 700 }).Publish(stream);

            Assert.True(stream.CanWrite);
            Assert.True(stream.Length > 0);
        }

        /// <summary>
        /// A stream that can only be written to, like an HTTP response body
        /// </summary>
        private class WriteOnlyStream : Stream
        {
            private readonly Stream _inner;
            public WriteOnlyStream(Stream inner) => _inner = inner;
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() => _inner.Flush();
            public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => _inner.Write(buffer, offset, count);
        }
    }
}
