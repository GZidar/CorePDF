using System;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;
using System.IO;
using CorePDF.Pages;
using CorePDF.Contents;
using System.Collections.Generic;

namespace CorePDF.Editor.API.Controllers
{
    [Route("api/[controller]")]
    public class EditorController : Controller
    {
        // GET api/editor
        [HttpGet]
        public IActionResult Get()
        {
            var result = new Document();
            result.Properties.Title = "your document title";
            result.Properties.Author = "your name";
            result.Properties.CreationDate = DateTime.Now;

            result.HeadersFooters = new List<HeaderFooter>()
            {
                new HeaderFooter()
                {
                    Name = "header-001",
                    Contents = new List<Content>()
                    {
                        new TextBox()
                    }
                },
                new HeaderFooter()
                {
                    Name = "footer-001",
                    Contents = new List<Content>()
                    {
                        new Shape()
                        {
                            Type = Polygon.Line
                        }
                    }
                }
            };

            result.Pages = new List<Page>()
            {
                new Page()
                {
                    HeaderName = "header-001",
                    FooterName = "footer-001",
                    Contents = new List<Content>()
                    {
                        new Image(),
                        new Barcode()
                    }
                }
            };

            result.Images = new List<Embeds.ImageFile>()
            {
                new Embeds.ImageFile()
            };

            return new JsonResult(new { documentData = JsonSerializer.Serialize(result, DocumentJson.Options) });
        }

        // POST api/editor
        [HttpPost]
        public IActionResult Post([FromBody]string documentData = "")
        {
            Document document;
            try
            {
                document = JsonSerializer.Deserialize<Document>(documentData, DocumentJson.Options);
            }
            catch (JsonException ex)
            {
                return BadRequest(new { error = $"The document JSON is invalid: {ex.Message}" });
            }

            if (document == null)
            {
                return BadRequest(new { error = "No document was provided." });
            }

            using (var memstream = new MemoryStream())
            {
                document.Publish(memstream);
                var result = Convert.ToBase64String(memstream.ToArray());

                Response.ContentType = "application/pdf";
                return new JsonResult(new { pdf = result });
            }
        }
    }
}
