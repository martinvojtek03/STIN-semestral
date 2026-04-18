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
            // Načteme kurzy seřazené podle abecedy
            var rates = await _context.Currencies.OrderBy(c => c.name).ToListAsync();

            // Načteme metadata (poslední aktualizaci)
            var meta = await _context.Metadata.FirstOrDefaultAsync();

            // Pokud je databáze prázdná, rates bude prázdný seznam (nebude to házet chybu)
            ViewBag.LastUpdate = meta?.LastUpdate;

            // Předáme seznam měn i pro dropdown v kalkulačce
            ViewBag.CurrencyList = rates;

            return View(rates);
        }

        // --- TLAČÍTKO AKTUALIZOVAT ---
        [HttpPost]
        public async Task<IActionResult> UpdateRates()
        {
            try
            {
                await _exchangeService.UpdateDatabaseRatesAsync();
                TempData["Success"] = "Data byla úspěšně stažena z API.";
            }
            catch (Exception ex)
            {
                TempData["Error"] = $"Chyba při aktualizaci: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // --- LOGIKA KALKULAČKY ---
        [HttpPost]
        public async Task<IActionResult> Convert(decimal amount, string fromCurrency, string toCurrency)
        {
            var rates = await _context.Currencies.ToListAsync();
            var rateFrom = rates.FirstOrDefault(r => r.name == fromCurrency)?.price;
            var rateTo = rates.FirstOrDefault(r => r.name == toCurrency)?.price;

            if (rateFrom.HasValue && rateTo.HasValue && rateFrom.Value != 0)
            {
                // Výpočet: (Částka / KurzZ) * KurzDo
                // Protože kurzy jsou vůči USD (1 USD = X měny)
                decimal result = (amount / rateFrom.Value) * rateTo.Value;

                ViewBag.Result = result;
                ViewBag.Amount = amount;
                ViewBag.From = fromCurrency;
                ViewBag.To = toCurrency;
            }

            // Vrátíme se na Index, ale tentokrát tam budeme mít v ViewBag výsledek
            ViewBag.LastUpdate = (await _context.Metadata.FirstOrDefaultAsync())?.LastUpdate;
            ViewBag.CurrencyList = rates;
            return View("Index", rates);
        }
    }
}