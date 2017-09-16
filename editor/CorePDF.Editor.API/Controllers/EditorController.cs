using System;
using Microsoft.AspNetCore.Mvc;
using Newtonsoft.Json;

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

            return new JsonResult(new { documentData = JsonConvert.SerializeObject(result) });
        }

        // POST api/editor
        [HttpPost]
        public void Post([FromBody]string document = "")
        {

        }
    }
}
