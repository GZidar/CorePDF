using CorePDF.Contents;
using CorePDF.Embeds;
using CorePDF.Pages;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Xunit;

namespace CorePDF.IntegrationTests
{
    [Trait("Category", "Integration")]
    public class EmbeddedFontTests
    {
        [Fact]
        public void Publish_FontAddedToFontFilesDirectly_EmbedsFont()
        {
            var document = new Document();
            document.FontFiles.Add(CreateFontFile());
            document.Pages.Add(CreatePage());

            var pdf = Encoding.Latin1.GetString(Publish(document));

            // embedded fonts are subset, so their name carries a six letter subset tag
            Assert.Matches(@"/BaseFont /[A-Z]{6}\+GreatVibes-Regular", pdf);
            Assert.Contains("/FontFile2", pdf);
        }

        [Fact]
        public void Publish_AfterJsonRoundTrip_ProducesSameOutput()
        {
            var original = new Document();
            original.EmbedFont(CreateFontFile());
            original.Pages.Add(new Page { PageSize = Paper.PAGEA4PORTRAIT });

            var json = JsonSerializer.Serialize(original);
            var roundTripped = JsonSerializer.Deserialize<Document>(json);

            Assert.Equal(Publish(original), Publish(roundTripped));
        }

        [Fact]
        public void Publish_Twice_ProducesSameOutput()
        {
            var document = new Document();
            document.EmbedFont(CreateFontFile());
            document.Pages.Add(new Page { PageSize = Paper.PAGEA4PORTRAIT });

            var first = Publish(document);
            var second = Publish(document);

            Assert.Equal(first, second);
        }

        private static byte[] Publish(Document document)
        {
            using (var stream = new MemoryStream())
            {
                document.Publish(stream);
                return stream.ToArray();
            }
        }

        private static Page CreatePage()
        {
            return new Page
            {
                PageSize = Paper.PAGEA4PORTRAIT,
                Contents = new List<Content>
                {
                    new TextBox { Text = "Embedded font", FontFace = "GreatVibes-Regular", FontSize = 20, PosX = 40, PosY = 400 }
                }
            };
        }

        private static FontFile CreateFontFile()
        {
            return new FontFile
            {
                Name = "GreatVibes-Regular",
                BaseFont = "GreatVibes",
                FilePath = "GreatVibes-Regular.ttf"
            };
        }
    }
}
