using System;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;
using System.IO;
using CorePDF.Pages;
using CorePDF.Contents;
using System.Collections.Generic;

namespace CorePDF.Editor.API.Controllers
{
    [Route("api/[controller]")]
    public class EditorController : Controller
    {
        private JsonSerializerSettings _settings = new JsonSerializerSettings
        {
            TypeNameHandling = TypeNameHandling.Objects
        };

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
                        new Image()
                    }
                }
            };

            result.Images = new List<Embeds.ImageFile>()
            {
                new Embeds.ImageFile()
            };

            return new JsonResult(new { documentData = JsonConvert.SerializeObject(result, _settings) });
        }

        // POST api/editor
        [HttpPost]
        public IActionResult Post([FromBody]string documentData = "")
        {
            var document = JsonConvert.DeserializeObject<Document>(documentData, _settings);

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
