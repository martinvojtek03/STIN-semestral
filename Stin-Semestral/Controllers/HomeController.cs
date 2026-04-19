using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stin_Semestral.Data;
using Stin_Semestral.Models;
using Stin_Semestral.Services;
using System.Text.Json;

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

        // --- HLAVNÍ STRÁNKA (Veřejná) ---
        [Authorize]
        public async Task<IActionResult> Index()
        {
            // 1. Načtení nastavení
            var settings = await _context.Settings.FirstOrDefaultAsync()
                           ?? new UserSettings { BaseCurrency = "USD", SelectedCurrencies = "CZK,EUR,USD" };

            var watched = settings.SelectedCurrencies?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();
            DateTime thirtyDaysAgo = DateTime.Today.AddDays(-30);

            // 2. Načtení dat
            var relevantCurrencies = watched.ToList();
            if (!relevantCurrencies.Contains(settings.BaseCurrency))
                relevantCurrencies.Add(settings.BaseCurrency);

            var allData = await _context.Currencies
                .Where(c => relevantCurrencies.Contains(c.Name) && c.Date >= thirtyDaysAgo)
                .ToListAsync();

            if (!allData.Any()) return View(new List<CurrencyViewModel>());

            // 3. Příprava mapy kurzů
            var baseCurrencyMap = allData
                .Where(c => c.Name == settings.BaseCurrency)
                .GroupBy(c => c.Date.Date)
                .ToDictionary(g => g.Key, g => g.First().Rate);

            var latestDate = allData.Max(c => c.Date);

            // 4. Výpočet statistik
            var displayData = new List<CurrencyViewModel>();

            foreach (var name in watched)
            {
                var history = allData.Where(c => c.Name == name).OrderBy(h => h.Date).ToList();
                var today = history.LastOrDefault();

                if (today == null) continue;

                var convertedRates = history
                    .Where(h => baseCurrencyMap.ContainsKey(h.Date.Date))
                    .Select(h => h.Rate / baseCurrencyMap[h.Date.Date])
                    .ToList();

                displayData.Add(new CurrencyViewModel
                {
                    Name = name,
                    CurrentRate = today.Rate / (baseCurrencyMap.GetValueOrDefault(today.Date.Date, 1.0m)),
                    Average30Days = convertedRates.Any() ? convertedRates.Average() : 0,
                    HistoryJson = JsonSerializer.Serialize(convertedRates.TakeLast(30))
                });
            }

            // 5. Nejsilnější a nejslabší měna
            var competitors = displayData.Where(d => d.Name != settings.BaseCurrency).ToList();
            if (competitors.Any())
            {
                ViewBag.Strongest = competitors.OrderBy(x => x.CurrentRate).First();
                ViewBag.Weakest = competitors.OrderByDescending(x => x.CurrentRate).First();
            }

            ViewBag.BaseCurrency = settings.BaseCurrency;
            ViewBag.LastUpdate = latestDate.ToString("dd.MM.yyyy HH:mm");
            return View(displayData);
        }

        // --- RUČNÍ AKTUALIZACE (Pouze pro přihlášené) ---
        [Authorize] // Tuto metodu smí volat jen přihlášený admin
        [HttpPost]
        public async Task<IActionResult> UpdateRates()
        {
            string json = await _exchangeService.GetExchangeRatesAsync();
            if (!string.IsNullOrEmpty(json))
            {
                await _exchangeService.UpdateDatabaseRatesAsync(json);
            }
            return RedirectToAction("Index");
        }

        // --- CHYBOVÁ STRÁNKA (Veřejná) ---
        [AllowAnonymous]
        [Route("Home/Error/{statusCode?}")]
        public IActionResult Error(int? statusCode)
        {
            if (statusCode.HasValue)
            {
                if (statusCode == 404)
                {
                    ViewBag.ErrorMessage = "Stránka, kterou hledáte, neexistuje.";
                    ViewBag.Icon = "bi-search";
                }
                else if (statusCode == 403)
                {
                    ViewBag.ErrorMessage = "Sem nemáte přístup. Tato sekce je pouze pro administrátory.";
                    ViewBag.Icon = "bi-shield-lock";
                }
                else
                {
                    ViewBag.ErrorMessage = "Něco se pokazilo na naší straně. Zkuste to prosím později.";
                    ViewBag.Icon = "bi-exclamation-triangle";
                }
                ViewBag.StatusCode = statusCode;
            }
            return View();
        }
    }
}