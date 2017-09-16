using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Http;

namespace CorePDF.Editor.API.Pages
{
    public class IndexModel : PageModel
    {
        public string applicationInsightsKey { get; set; }
        public string serviceHost { get; set; }

        public IndexModel(IConfiguration config)
        {
            applicationInsightsKey = config["APPINSIGHTS_INSTRUMENTATIONKEY"];
        }

        public void OnGet()
        {
            serviceHost = "";
            if (Request.IsHttps)
            {
                serviceHost += "https://";
            }
            else
            {
                serviceHost += "http://";
            }

            serviceHost += Request.Host.Value;
        }

    }
}