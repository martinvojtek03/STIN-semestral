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
            // 1. Získáme uživatelské nastavení (pokud neexistuje, vytvoříme default)
            var settings = await _context.Settings.FirstOrDefaultAsync();
            if (settings == null)
            {
                settings = new UserSettings { BaseCurrency = "EUR", SelectedCurrencies = "CZK,USD" };
                _context.Settings.Add(settings);
                await _context.SaveChangesAsync();
            }

            // 2. Převedeme string "CZK,USD" na seznam (List<string>) pro snadné filtrování
            var watchedCurrencies = settings.SelectedCurrencies
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(c => c.Trim().ToUpper())
                .ToList();

            // 3. Najdeme nejnovější datum v DB
            var latestDate = await _context.Currencies.AnyAsync()
                ? await _context.Currencies.MaxAsync(c => c.Date)
                : DateTime.MinValue;

            // 4. Načteme kurzy, které jsou v seznamu sledovaných měn
            var rates = await _context.Currencies
                .Where(c => c.Date == latestDate && watchedCurrencies.Contains(c.Name))
                .OrderBy(c => c.Name)
                .ToListAsync();

            ViewBag.LastUpdate = latestDate == DateTime.MinValue ? "Nikdy" : latestDate.ToString("g");
            ViewBag.BaseCurrency = settings.BaseCurrency; // Předáme informaci o hlavní měně

            return View(rates);
        }

        

        
    }
}