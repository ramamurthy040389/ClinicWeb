using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace Clinic.Web.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _config;
        public AccountController(IConfiguration config) => _config = config;

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            //ViewData["ReturnUrl"] = returnUrl ?? Url.Action("Appointments", "Admin");
            //return View();
            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(string username, string password, string? returnUrl = null)
        {
            // simple credential check from appsettings (dev only)
            var adminUser = _config["Admin:Username"];
            var adminPass = _config["Admin:Password"];

            if (string.Equals(username?.Trim(), adminUser, StringComparison.OrdinalIgnoreCase)
                && password == adminPass)
            {
                var claims = new List<Claim>
                {
                    new Claim(ClaimTypes.Name, username),
                    new Claim(ClaimTypes.Email, username),
                    new Claim(ClaimTypes.Role, "Admin")
                };

                var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                var principal = new ClaimsPrincipal(identity);

                await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, principal);

                if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                    return LocalRedirect(returnUrl);

                return RedirectToAction("Appointments", "Admin");
            }

            ModelState.AddModelError(string.Empty, "Invalid username or password");
            ViewData["ReturnUrl"] = returnUrl ?? Url.Action("Appointments", "Admin");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult AccessDenied()
        {
            return View();
        }
    }
}
