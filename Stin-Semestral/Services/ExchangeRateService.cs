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
        private readonly Logger _logger;

        public ExchangeRateService(ApplicationDbContext context, HttpClient httpClient, IConfiguration configuration, Logger logger)
        {
            _context = context;
            _httpClient = httpClient;
            _apiKey = configuration["ExchangeRateApi:ApiKey"] ?? "";
            _logger = logger;

        }


        public async Task<string> GetHistoricalRatesAsync(int daysBack)
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                await _logger.LogMessage("ERROR", "API klíč nebyl nalezen pro historická data.");
                return string.Empty;
            }

            try
            {
                // 1. Výpočet data (dnes minus počet dní)
                DateTime targetDate = DateTime.Today.AddDays(-daysBack);
                string dateString = targetDate.ToString("yyyy-MM-dd");

                // 2. Sestavení URL pro endpoint /historical
                // API dokumentace vyžaduje parametr 'date'
                string url = $"https://api.exchangerate.host/historical?access_key={_apiKey}&date={dateString}&source=USD";

                var response = await _httpClient.GetStringAsync(url);

                await _logger.LogMessage("INFO", $"Úspěšně stažena historická data pro den: {dateString}");
                return response;
            }
            catch (Exception ex)
            {
                await _logger.LogMessage("ERROR", $"Chyba při stahování historických dat (dní zpět: {daysBack}): {ex.Message}");
                return string.Empty;
            }
        }

        public async Task<string> GetExchangeRatesAsync() //stáhne aktuální kurzy měn
        {
            if (string.IsNullOrEmpty(_apiKey))
            {
                string errorMsg = "API klíč nebyl v konfiguraci nalezen!";
                Console.WriteLine($"CHYBA: {errorMsg}");
                await _logger.LogMessage("ERROR", errorMsg);
                return string.Empty;
            }

            try
            {
                string url = $"https://api.exchangerate.host/live?access_key={_apiKey}&source=USD";

                var response = await _httpClient.GetStringAsync(url);
                await _logger.LogMessage("INFO", $"Úspěšně staženy kurzy.");
                return response;
            }
            catch (Exception ex)
            {
                await _logger.LogMessage("ERROR", $"Chyba při stahování kurzů: {ex.Message}");
                return string.Empty;
            }
        }

        public async Task UpdateDatabaseRatesAsync(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText)) return;

            // Deserializace JSONu (používáme tvůj ExchangeRateResponse)
            var data = JsonSerializer.Deserialize<ExchangeRateResponse>(jsonText,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (data != null && data.Success)
            {
                try
                {
                    // Převod Unix timestampu z JSONu na DateTime
                    DateTime rateDate = DateTimeOffset.FromUnixTimeSeconds(data.Timestamp).DateTime;

                    foreach (var quote in data.Quotes)
                    {
                        // Uřezáváme "USD" z názvů (např. USDCZK -> CZK)
                        string currencyName = quote.Key.StartsWith("USD")
                            ? quote.Key.Substring(3)
                            : quote.Key;

                        // Vytvoříme nový záznam typu Currency
                        var newCurrencyRecord = new Currency
                        {
                            Name = currencyName,
                            Rate = quote.Value,
                            Date = rateDate // Ukládáme datum z JSONu
                        };

                        _context.Currencies.Add(newCurrencyRecord);
                    }
                    // Uložení všech změn najednou
                    await _context.SaveChangesAsync();

                    await _logger.LogMessage("INFO", $"Uloženo {data.Quotes.Count} kurzů pro datum {rateDate.ToShortDateString()}.");
                }
                catch (Exception ex)
                {
                    await _logger.LogMessage("ERROR", $"Chyba při zápisu Currency dat: {ex.Message}");
                }
            }
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