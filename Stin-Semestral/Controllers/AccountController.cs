using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc;
using Stin_Semestral.Services;
using System.Security.Claims;

namespace Stin_Semestral.Controllers
{
    public class AccountController : Controller
    {
        private readonly IConfiguration _config;
        private readonly PasswordService _passwordService;

        public AccountController(IConfiguration config, PasswordService passwordService)
        {
            _config = config;
            _passwordService = passwordService;
        }

        [HttpGet]
        public IActionResult Login() => View();

        [HttpPost]
        public async Task<IActionResult> Login(string username, string password)
        {
            // Načtení hashe z User Secrets (nebo appsettings)
            var storedHash = _config["AdminSettings:PasswordHash"];

            // Ověření jména (natvrdo "admin") a hesla přes službu
            if (username == "admin" && !string.IsNullOrEmpty(storedHash) &&
                _passwordService.VerifyPassword(storedHash, password))
            {
                var claims = new List<Claim> { new Claim(ClaimTypes.Name, "admin") };
                var identity = new ClaimsIdentity(claims, "CookieAuth");
                var principal = new ClaimsPrincipal(identity);

                // Vytvoření přihlašovací cookie
                await HttpContext.SignInAsync("CookieAuth", principal);

                return RedirectToAction("Index", "Home");
            }

            ViewBag.Error = "Neplatné přihlašovací údaje.";
            return View();
        }

        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync("CookieAuth");
            return RedirectToAction("Index", "Home");
        }
    }
}