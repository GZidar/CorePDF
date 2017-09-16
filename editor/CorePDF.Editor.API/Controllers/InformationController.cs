using System;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.Features;

namespace CorePDF.Editor.API.Controllers
{
    /// <summary>
    /// This controller is responsible for handling the request for application information
    /// </summary>
    [Route("api/[controller]")]
    public class InformationController : Controller
    {
        /// <summary>
        /// Returns the application information
        /// </summary>
        [HttpGet]
        public IActionResult Get()
        {
            return new JsonResult(new
            {
                ApplicationVersion = "0.1.0",
                ClientIPAddress = GetClientIPAddress(),
                ServiceHost = GetServiceHost(),
                ReferrerHost = GetReferrerHost()
            });
        }

        protected string GetServiceHost()
        {
            var result = "";
            if (Request.IsHttps)
            {
                result += "https://";
            }
            else
            {
                result += "http://";
            }

            result += Request.Host.Value;

            return result;
        }

        protected string GetReferrerHost()
        {
            var result = Request.Headers["Requester"].ToString();
            if (string.IsNullOrEmpty(result))
            {
                return GetServiceHost();
            }

            var referrer = new Uri(result);
            var host = referrer.GetLeftPart(UriPartial.Authority);
            return host;
        }

        protected string GetClientIPAddress()
        {
            var remoteIpAddress = HttpContext.Features.Get<IHttpConnectionFeature>()?.RemoteIpAddress;
            return remoteIpAddress?.ToString();
        }
    }

}
