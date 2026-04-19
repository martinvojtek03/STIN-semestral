using Microsoft.EntityFrameworkCore;
using Stin_Semestral.Data;
using Stin_Semestral.Services;
using Stin_Semestral.Models;
using Stin_Semestral.BackgroundServices;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args);

// --- 1. REGISTRACE SLUŽEB ---

// Databáze
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=stin_database.db"));

builder.Services.AddHttpClient();

// Tvůj vlastní Logger, Služby a PasswordService
builder.Services.AddScoped<Logger>();
builder.Services.AddScoped<ExchangeRateService>();
builder.Services.AddSingleton<PasswordService>();

builder.Services.AddControllersWithViews();

// Background service pro automatické stahování kurzů
builder.Services.AddHostedService<ExchangeRateBackgroundService>();

// Autentizace pomocí Cookies
builder.Services.AddAuthentication("CookieAuth")
    .AddCookie("CookieAuth", config =>
    {
        config.Cookie.Name = "Admin.Cookie";
        config.LoginPath = "/Account/Login"; // Kam přesměrovat nepřihlášeného uživatele
        config.AccessDeniedPath = "/Home/Index";
    });

var app = builder.Build();

// --- 2. MIDDLEWARE ---

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseStaticFiles();
app.UseRouting();

// Pořadí je důležité: nejdřív Authentication, pak Authorization
app.UseAuthentication();
app.UseAuthorization();

// --- 3. INICIALIZACE DATABÁZE A KONTROLA DAT ---
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var logger = services.GetRequiredService<Logger>();

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var exchangeService = services.GetRequiredService<ExchangeRateService>();

        // Vytvoří DB, pokud neexistuje
        await context.Database.EnsureCreatedAsync();

        // Základní nastavení (pokud v DB chybí)
        var settings = await context.Settings.FirstOrDefaultAsync();
        if (settings == null)
        {
            settings = new UserSettings { BaseCurrency = "USD", SelectedCurrencies = "CZK,EUR,USD" };
            context.Settings.Add(settings);
            await context.SaveChangesAsync();
        }

        // Kontrola, zda máme historická data pro grafy (posledních 30 dní)
        var selectedCurrencies = settings.SelectedCurrencies?
            .Split(',', StringSplitOptions.RemoveEmptyEntries).ToList() ?? new List<string>();

        DateTime startDate = DateTime.Today.AddDays(-30);

        var dataCheck = await context.Currencies
            .Where(c => c.Date.Date >= startDate && selectedCurrencies.Contains(c.Name))
            .GroupBy(c => c.Name)
            .Select(g => new { Name = g.Key, Count = g.Select(x => x.Date.Date).Distinct().Count() })
            .Where(x => x.Count >= 28)
            .CountAsync();

        if (dataCheck < selectedCurrencies.Count)
        {
            await logger.LogMessage("INFO", "Při startu chybí historická data, spouštím doplňování...");
            await exchangeService.UpdateDatabaseTimeframeAsync(30);

            string liveJson = await exchangeService.GetExchangeRatesAsync();
            if (!string.IsNullOrEmpty(liveJson))
            {
                await exchangeService.UpdateDatabaseRatesAsync(liveJson);
            }
        }
    }
    catch (Exception ex)
    {
        try { await logger.LogMessage("ERROR", $"Chyba při inicializaci aplikace: {ex.Message}"); }
        catch { /* Pokud selže i logger, neděláme nic */ }
    }
}

// --- 4. ROUTOVÁNÍ ---

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();