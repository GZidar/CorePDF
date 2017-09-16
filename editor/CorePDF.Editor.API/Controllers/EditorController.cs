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
        // GET api/editor
        [HttpGet]
        public IActionResult Get()
        {
            var result = new Document();
            result.Properties.Title = "your document title";
            result.Properties.Author = "your name";
            result.Properties.CreationDate = DateTime.Now;

            result.Pages.Add(new Page
            {
                PageSize = Paper.PAGEA4PORTRAIT,
                Contents = new List<Content>()
                {
                    new TextBox
                    {
                        Text = "This is a test document",
                        FontSize = 30,
                        PosX = 250,
                        PosY = 400,
                        TextAlignment = Alignment.Center
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

            return new JsonResult(new { documentData = JsonConvert.SerializeObject(result) });
        }

        // POST api/editor
        [HttpPost]
        public IActionResult Post([FromBody]string documentData = "")
        {
            var document = JsonConvert.DeserializeObject<Document>(documentData);

            using (var memstream = new MemoryStream())
            {
                document.Publish(memstream);
                var result = Convert.ToBase64String(memstream.ToArray());

                return new JsonResult(new { pdf = result });
            }
        }
    }
}
