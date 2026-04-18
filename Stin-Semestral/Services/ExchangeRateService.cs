using System.Text.Json;
using Stin_Semestral.Data;
using Stin_Semestral.Models;
using Microsoft.Extensions.Configuration;
using Microsoft.EntityFrameworkCore;

namespace Stin_Semestral.Services
{
    public class ExchangeRateService
    {
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        public ExchangeRateService(ApplicationDbContext context, HttpClient httpClient, IConfiguration configuration)
        {
            _context = context;
            _httpClient = httpClient;
            _apiKey = configuration["ExchangeRateApi:ApiKey"] ?? "";
        }

        public async Task<string> GetExchangeRatesAsync()
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                string errorMsg = "API klíč nebyl v konfiguraci nalezen!";
                Console.WriteLine($"CHYBA: {errorMsg}");
                await LogMessage("ERROR", errorMsg);
                return string.Empty;
            }

            try
            {
                // Používáme tvou URL, source můžeš měnit dle potřeby (USD/EUR)
                string url = $"https://api.exchangerate.host/live?access_key={_apiKey}&source=USD";

                var response = await _httpClient.GetStringAsync(url);
                await LogMessage("INFO", $"Úspěšně staženy kurzy.");
                return response;
            }
            catch (Exception ex)
            {
                await LogMessage("ERROR", $"Chyba při stahování kurzů: {ex.Message}");
                return string.Empty;
            }
        }

        public async Task UpdateDatabaseRatesAsync()
{
    // 1. Vždy stahujeme vůči USD
    var jsonText = await GetExchangeRatesAsync();
    
    if (string.IsNullOrEmpty(jsonText)) return;

    var data = JsonSerializer.Deserialize<ExchangeRateResponse>(jsonText, 
        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

    if (data != null && data.Success)
    {
        try 
        {
            var oldRates = await _context.Currencies.ToListAsync();
            _context.Currencies.RemoveRange(oldRates);

            foreach (var quote in data.Quotes)
            {
                // Teď uřezáváme "USD" z názvů (např. USDCZK -> CZK)
                string currencyName = quote.Key.StartsWith("USD") 
                    ? quote.Key.Substring(3) 
                    : quote.Key;

                _context.Currencies.Add(new Currency
                {
                    name = currencyName,
                    price = quote.Value // Toto je teď kurz vůči 1 USD
                });
            }

            var metadata = await _context.Metadata.FirstOrDefaultAsync() ?? new ApiMetadata();
            metadata.LastUpdate = DateTimeOffset.FromUnixTimeSeconds(data.Timestamp).DateTime;
            
            if (metadata.Id == 0) _context.Metadata.Add(metadata);

            await _context.SaveChangesAsync();
            await LogMessage("INFO", "Databáze kurzů (základ USD) byla aktualizována.");
        }
        catch (Exception ex)
        {
            await LogMessage("ERROR", $"Chyba při ukládání USD kurzů: {ex.Message}");
        }
    }
}

        public async Task LogMessage(string level, string message)
        {
            var log = new ExchangeLog
            {
                Timestamp = DateTime.Now,
                Level = level,
                Message = message
            };

            _context.Logs.Add(log);
            await _context.SaveChangesAsync();
        }
    }

    // Pomocné třídy pro mapování JSONu z API
    public class ExchangeRateResponse
    {
        public bool Success { get; set; }
        public long Timestamp { get; set; }
        public string Source { get; set; } = string.Empty;
        public Dictionary<string, decimal> Quotes { get; set; } = new();
    }
}