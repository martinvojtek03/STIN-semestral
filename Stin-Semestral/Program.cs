using Microsoft.EntityFrameworkCore;
using Stin_Semestral.Data;
using Stin_Semestral.Services;

var builder = WebApplication.CreateBuilder(args);

// --- 1. REGISTRACE SLUŽEB (Dependency Injection) ---

// Registrace databáze
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=stin_database.db"));

// Registrace HttpClient
builder.Services.AddHttpClient();

// REGISTRACE LOGGERU - Toto ti tam chybělo!
builder.Services.AddScoped<Logger>();

// Registrace tvého Exchange servisu
builder.Services.AddScoped<ExchangeRateService>();

// Podpora pro MVC (Controllery a View)
builder.Services.AddControllersWithViews();

var app = builder.Build();

// --- 2. MIDDLEWARE ---

app.UseStaticFiles();
app.UseRouting();

// --- 3. AUTOMATICKÁ KONTROLA DAT PŘI STARTU ---

using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    // Vytáhneme logger hned na začátku, abychom ho měli pro catch blok
    var logger = services.GetRequiredService<Logger>();

    try
    {
        var context = services.GetRequiredService<ApplicationDbContext>();
        var exchangeService = services.GetRequiredService<ExchangeRateService>();

        // Zajistí, že databáze a tabulky existují
        await context.Database.EnsureCreatedAsync();

        // Podíváme se, zda máme v DB data pro dnešní den
        DateTime today = DateTime.Today;
        bool hasTodayData = await context.Currencies.AnyAsync(c => c.Date.Date == today);

        if (!hasTodayData)
        {
            await logger.LogMessage("INFO", "Dnešní data nenalezena, stahuji z API...");

            // Stáhneme JSON string (aktuální kurzy)
            string json = await exchangeService.GetExchangeRatesAsync();

            if (!string.IsNullOrEmpty(json))
            {
                // Zpracujeme a uložíme do DB
                await exchangeService.UpdateDatabaseRatesAsync(json);
                await logger.LogMessage("INFO", "Dnešní data byla úspěšně stažena a uložena.");
            }
        }
        else
        {
            // Volitelné: log do konzole, že je vše OK, ať víš, že program běží
            Console.WriteLine("Data jsou aktuální. Startuji aplikaci...");
        }
    }
    catch (Exception ex)
    {
        // Vypíšeme do konzole a zkusíme zalogovat do DB
        Console.WriteLine($"KRITICKÁ CHYBA PŘI STARTU: {ex.Message}");
        try
        {
            await logger.LogMessage("ERROR", $"KRITICKÁ CHYBA PŘI STARTU: {ex.Message}");
        }
        catch
        {
            /* Pokud selhala DB, víc už nenaděláme */
        }
    }
}

// --- 4. ROUTOVÁNÍ ---

app.MapGet("/status", () => Results.Content("<h1>Status: OK</h1>", "text/html"));

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();