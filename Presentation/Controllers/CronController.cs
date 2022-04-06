using Microsoft.AspNetCore.Mvc;
using RestSharp;
using RestSharp.Authenticators;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Presentation.Controllers
{
    public class CronController : Controller
    {
        public async Task<IActionResult> test()
        {
            RestClient client = new RestClient("https://api.mailgun.net/v3");
            client.Authenticator = new HttpBasicAuthenticator("api", "30044dc3b65a3c4450bf4206ef1ed55e-90346a2d-89c1134c");
            RestRequest request = new RestRequest();
            request.AddParameter("domain", "sandboxd969183d955243d7b2eabde888924a6b.mailgun.org", ParameterType.UrlSegment);
            request.Resource = "{domain}/messages";
            request.AddParameter("from", "ryanattarddemo@gmail.com");
            request.AddParameter("to", "ryanattard@gmail.com");
            request.AddParameter("subject", "CronJob 1 : Testing Mail Feature");
            request.AddParameter("text", "This is just a test");
            request.Method = Method.Post;
            await client.ExecuteAsync(request);

            return Ok("Email sent"); // Content("Done Email Sent");
        }
    }
}
