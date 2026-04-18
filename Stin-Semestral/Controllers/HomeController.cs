using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stin_Semestral.Data;
using Stin_Semestral.Models;
using Stin_Semestral.Services;

namespace Stin_Semestral.Controllers
{
    public class HomeController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ExchangeRateService _exchangeService;

        public HomeController(ApplicationDbContext context, ExchangeRateService exchangeService)
        {
            _context = context;
            _exchangeService = exchangeService;
        }

        // --- HLAVNÍ STRÁNKA ---
        public async Task<IActionResult> Index()
        {
            // Načtení nastavení z _context.Settings
            var settings = await _context.Settings.FirstOrDefaultAsync()
                           ?? new UserSettings { BaseCurrency = "USD", SelectedCurrencies = "CZK,EUR,USD" };

            if (!await _context.Currencies.AnyAsync()) return View(new List<Currency>());

            var latestDate = await _context.Currencies.MaxAsync(c => c.Date);
            var allRates = await _context.Currencies.Where(c => c.Date == latestDate).ToListAsync();

            // Kurz základní měny (např. EUR) vůči USD pro přepočet
            var baseCurrencyObj = allRates.FirstOrDefault(r => r.Name == settings.BaseCurrency);
            decimal baseValue = (baseCurrencyObj != null && baseCurrencyObj.Rate != 0) ? baseCurrencyObj.Rate : 1.0m;

            var watched = settings.SelectedCurrencies?.Split(',') ?? new string[0];

            // Výpočet kurzů vůči zvolené základní měně
            var displayRates = allRates
                .Where(r => watched.Contains(r.Name))
                .Select(r => new Currency
                {
                    Name = r.Name,
                    Rate = r.Rate / baseValue, // Přepočet: (Cílový kurz / Základní kurz)
                    Date = r.Date
                })
                .OrderBy(r => r.Name)
                .ToList();

            ViewBag.BaseCurrency = settings.BaseCurrency;
            ViewBag.LastUpdate = latestDate.ToString("g");

            return View(displayRates);
        }




    }
}