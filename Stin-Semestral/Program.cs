using Microsoft.EntityFrameworkCore;
using Stin_Semestral.Data;
using Stin_Semestral.Services;
using Stin_Semestral.Models;
using Stin_Semestral.BackgroundServices;

var builder = WebApplication.CreateBuilder(args);

// --- 1. REGISTRACE SLUŽEB ---
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=stin_database.db"));

builder.Services.AddHttpClient();
builder.Services.AddScoped<Logger>();
builder.Services.AddScoped<ExchangeRateService>();
builder.Services.AddControllersWithViews();
builder.Services.AddHostedService<ExchangeRateBackgroundService>();

var app = builder.Build();

// --- 2. MIDDLEWARE ---
app.UseStaticFiles();
app.UseRouting();

// --- 3. AUTOMATICKÁ KONTROLA DAT (Dle aktuálního nastavení) ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<Logger>();

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var exchangeService = services.GetRequiredService<ExchangeRateService>();

        await context.Database.EnsureCreatedAsync();

        // A. Získání AKTUÁLNÍHO nastavení
        var settings = await context.Settings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new UserSettings { BaseCurrency = "USD", SelectedCurrencies = "CZK,EUR,USD" };
            context.Settings.Add(settings);
            await context.SaveChangesAsync();
        }

        // B. Rozklad vybraných měn na seznam
        var selectedCurrencies = settings.SelectedCurrencies?
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .ToList() ?? new List<string>();

        // C. Kontrola, zda máme pro tyto měny data za posledních 30 dní
        DateTime startDate = DateTime.Today.AddDays(-30);

        // Zjistíme, kolik z vybraných měn má v DB kompletní historii (alespoň 28 záznamů)
        var currenciesWithDataCount = await context.Currencies
            .Where(c => c.Date.Date >= startDate && selectedCurrencies.Contains(c.Name))
            .GroupBy(c => c.Name)
            .Select(g => new { Name = g.Key, Count = g.Select(x => x.Date.Date).Distinct().Count() })
            .Where(x => x.Count >= 28)
            .CountAsync();

        // Pokud nemáme kompletní data pro všechny vybrané měny
        if (currenciesWithDataCount < selectedCurrencies.Count)
        {
            await logger.LogMessage("INFO", $"Chybí historická data pro některé z {selectedCurrencies.Count} vybraných měn. Stahuji timeframe...");

            await exchangeService.UpdateDatabaseTimeframeAsync(30);

            string liveJson = await exchangeService.GetExchangeRatesAsync();
            if (!string.IsNullOrEmpty(liveJson))
            {
                await exchangeService.UpdateDatabaseRatesAsync(liveJson);
            }

            await logger.LogMessage("INFO", "Data pro aktuálně nastavené měny byla úspěšně doplněna.");
        }
    }
    catch (Exception ex)
    {
        try { await logger.LogMessage("ERROR", $"KRITICKÁ CHYBA PŘI STARTU: {ex.Message}"); }
        catch { }
    }
}

// --- 4. ROUTOVÁNÍ ---
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();