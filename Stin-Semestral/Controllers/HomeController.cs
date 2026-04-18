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

        // --- HLAVNÍ STRÁNKA ---
        public async Task<IActionResult> Index()
        {
            // 1. Načtení nastavení (Perzistence dle bodu 3.4)
            var settings = await _context.Settings.FirstOrDefaultAsync()
                           ?? new UserSettings { BaseCurrency = "USD", SelectedCurrencies = "CZK,EUR,USD" };

            var watched = settings.SelectedCurrencies?.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();
            DateTime thirtyDaysAgo = DateTime.Today.AddDays(-30);

            // 2. Načtení všech potřebných dat jedním dotazem (Rychlost dle bodu 3.5)
            // Načítáme historii sledovaných měn + historii základní měny pro přepočet
            var relevantCurrencies = watched.ToList();
            if (!relevantCurrencies.Contains(settings.BaseCurrency))
                relevantCurrencies.Add(settings.BaseCurrency);

            var allData = await _context.Currencies
                .Where(c => relevantCurrencies.Contains(c.Name) && c.Date >= thirtyDaysAgo)
                .ToListAsync();

            if (!allData.Any()) return View(new List<CurrencyViewModel>());

            // 3. Příprava mapy kurzů základní měny pro bleskový přepočet
            var baseCurrencyMap = allData
                .Where(c => c.Name == settings.BaseCurrency)
                .GroupBy(c => c.Date.Date)
                .ToDictionary(g => g.Key, g => g.First().Rate);

            var latestDate = allData.Max(c => c.Date);

            // 4. Výpočet statistik (Bod 3.3 - Aritmetický průměr)
            var displayData = new List<CurrencyViewModel>();

            foreach (var name in watched)
            {
                var history = allData.Where(c => c.Name == name).ToList();
                var today = history.OrderByDescending(h => h.Date).FirstOrDefault();

                if (today == null) continue;

                // Přepočet všech historických bodů vůči základní měně daného dne
                var convertedRates = history
                    .Where(h => baseCurrencyMap.ContainsKey(h.Date.Date))
                    .Select(h => h.Rate / baseCurrencyMap[h.Date.Date])
                    .ToList();

                displayData.Add(new CurrencyViewModel
                {
                    Name = name,
                    CurrentRate = today.Rate / (baseCurrencyMap.GetValueOrDefault(today.Date.Date, 1.0m)),
                    Average30Days = convertedRates.Any() ? convertedRates.Average() : 0,
                    HistoryJson = JsonSerializer.Serialize(convertedRates.TakeLast(10)) // Pro malý graf
                });
            }

            // 5. Nejsilnější a nejslabší měna (Bod 3.2 - Dle nominální hodnoty)
            // Vynecháváme základní měnu, protože ta je vždy 1.0
            var competitors = displayData.Where(d => d.Name != settings.BaseCurrency).ToList();
            if (competitors.Any())
            {
                ViewBag.Strongest = competitors.OrderByDescending(x => x.CurrentRate).First();
                ViewBag.Weakest = competitors.OrderBy(x => x.CurrentRate).First();
            }

            ViewBag.BaseCurrency = settings.BaseCurrency;
            return View(displayData);
        }




    }
}