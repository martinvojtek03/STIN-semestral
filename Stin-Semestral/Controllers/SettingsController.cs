using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stin_Semestral.Data;
using Stin_Semestral.Models;

namespace Stin_Semestral.Controllers
{
    [Authorize]
    public class SettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Načtení z _context.Settings
            var settings = await _context.Settings.FirstOrDefaultAsync();

            if (settings == null)
            {
                settings = new UserSettings { BaseCurrency = "USD", SelectedCurrencies = "CZK,EUR" };
            }

            // Seznam měn pro checkboxy
            var allCurrencyNames = await _context.Currencies
                .Select(c => c.Name)
                .Distinct()
                .OrderBy(c => c)
                .ToListAsync();

            ViewBag.AllCurrencies = allCurrencyNames;
            return View(settings);
        }




        [HttpPost]
        public async Task<IActionResult> Save(string baseCurrency, List<string> selectedCurrencies)
        {
            var settings = await _context.Settings.FirstOrDefaultAsync();

            if (settings == null)
            {
                settings = new UserSettings();
                _context.Settings.Add(settings);
            }

            settings.BaseCurrency = baseCurrency;
            settings.SelectedCurrencies = selectedCurrencies != null
                ? string.Join(",", selectedCurrencies)
                : "";

            await _context.SaveChangesAsync();

            // Změna: Místo zpět do nastavení uživatele pošleme na hlavní přehled
            return RedirectToAction("Index", "Home");
        }
    }
}