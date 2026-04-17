using Microsoft.EntityFrameworkCore;
using Stin_Semestral.Data;
using Stin_Semestral.Services;

var builder = WebApplication.CreateBuilder(args);

// 1. Registrace databáze
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlite("Data Source=stin_database.db"));

// 2. Registrace HttpClient a Servisů
builder.Services.AddHttpClient();
builder.Services.AddScoped<ExchangeRateService>();

// --- PŘIDÁNO: Podpora pro Controllery a View ---
builder.Services.AddControllersWithViews();

var app = builder.Build();

// --- PŘIDÁNO: Middleware pro statické soubory (CSS, JS) a routování ---
app.UseStaticFiles();
app.UseRouting();

// Tvůj testovací endpoint pro API (ponechán)
app.MapGet("/test-api", async (ExchangeRateService service, IConfiguration config) =>
{
    var keyCheck = config["ExchangeRateApi:ApiKey"];
    var data = await service.GetExchangeRatesAsync();
    return string.IsNullOrEmpty(data) ? Results.Problem("Chyba API") : Results.Content(data, "application/json");
});

// Tvůj status endpoint (ponechán)
app.MapGet("/status", () => Results.Content("<h1>Status: OK</h1>", "text/html"));

// --- PŘIDÁNO: Mapování Controllerů (toto propojí HomeController s adresou /) ---
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();