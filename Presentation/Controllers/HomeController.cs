
using Google.Cloud.Diagnostics.AspNetCore3;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Presentation.Models;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace Presentation.Controllers
{
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private readonly IExceptionLogger _exceptionLogger;

        public HomeController(ILogger<HomeController> logger,
          IExceptionLogger exceptionLogger)
        {
            _logger = logger;
            _exceptionLogger = exceptionLogger;
        }

        public IActionResult Index()
        {
            _logger.LogInformation("Accessed the Index method");
            _logger.LogWarning("An error will be thrown on purpose to test");
            try
            {
                throw new Exception("testing error reporting");
            }
            catch(Exception ex)
            {
                _exceptionLogger.Log(ex);
            }
            return View();
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }


        [Authorize]
        public IActionResult Login()
        {
            return RedirectToAction("Index");
        }

        [Authorize]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync();
            return RedirectToAction("Index");
        }
    }
}
