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
                DateTime targetDate = DateTime.Today.AddDays(-daysBack);
                string dateString = targetDate.ToString("yyyy-MM-dd");

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

        public async Task<string> GetExchangeRatesAsync()
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

        public async Task UpdateDatabaseTimeframeAsync(int daysBack)
        {
            if (string.IsNullOrEmpty(_apiKey)) return;

            try
            {
                string startDate = DateTime.Today.AddDays(-daysBack).ToString("yyyy-MM-dd");
                string endDate = DateTime.Today.AddDays(-1).ToString("yyyy-MM-dd");

                string url = $"https://api.exchangerate.host/timeframe?access_key={_apiKey}&start_date={startDate}&end_date={endDate}&source=USD";
                var response = await _httpClient.GetStringAsync(url);

                // Ošetření deserializace i zde pro lepší coverage
                var data = JsonSerializer.Deserialize<TimeframeResponse>(response,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data != null && data.Success)
                {
                    foreach (var dateEntry in data.Quotes)
                    {
                        DateTime currentDay = DateTime.Parse(dateEntry.Key);
                        var dayQuotes = dateEntry.Value;

                        if (!dayQuotes.ContainsKey("USDUSD")) dayQuotes.Add("USDUSD", 1.0m);

                        foreach (var quote in dayQuotes)
                        {
                            string currencyName = quote.Key.Length > 3 && quote.Key.StartsWith("USD")
                                ? quote.Key.Substring(3)
                                : quote.Key;

                            if (string.IsNullOrEmpty(currencyName)) currencyName = "USD";

                            _context.Currencies.Add(new Currency
                            {
                                Name = currencyName,
                                Rate = quote.Value,
                                Date = currentDay
                            });
                        }
                    }

                    await _context.SaveChangesAsync();
                    await _logger.LogMessage("INFO", $"Historie za posledních {daysBack} dní úspěšně stažena a uložena.");
                }
            }
            catch (JsonException ex)
            {
                await _logger.LogMessage("ERROR", $"Neplatný JSON formát v timeframe: {ex.Message}");
            }
            catch (Exception ex)
            {
                await _logger.LogMessage("ERROR", $"Chyba při stahování timeframe: {ex.Message}");
            }
        }

        public async Task UpdateDatabaseRatesAsync(string jsonText)
        {
            if (string.IsNullOrEmpty(jsonText)) return;

            try
            {
                // Deserializace uvnitř try zachytí neplatné vstupy z testů
                var data = JsonSerializer.Deserialize<ExchangeRateResponse>(jsonText,
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (data != null && data.Success)
                {
                    DateTime rateDate = DateTimeOffset.FromUnixTimeSeconds(data.Timestamp).DateTime;

                    if (data.Quotes != null && !data.Quotes.ContainsKey("USDUSD") && !data.Quotes.ContainsKey("USD"))
                    {
                        data.Quotes.Add("USDUSD", 1.0m);
                    }

                    if (data.Quotes != null)
                    {
                        foreach (var quote in data.Quotes)
                        {
                            string currencyName = quote.Key.Length > 3 && quote.Key.StartsWith("USD")
                                ? quote.Key.Substring(3)
                                : quote.Key;

                            if (string.IsNullOrEmpty(currencyName)) currencyName = "USD";

                            var newCurrencyRecord = new Currency
                            {
                                Name = currencyName,
                                Rate = quote.Value,
                                Date = rateDate
                            };

                            _context.Currencies.Add(newCurrencyRecord);
                        }

                        await _context.SaveChangesAsync();
                        await _logger.LogMessage("INFO", $"Uloženo {data.Quotes.Count} kurzů pro datum {rateDate.ToShortDateString()}.");
                    }
                }
            }
            catch (JsonException ex)
            {
                await _logger.LogMessage("ERROR", $"Neplatný formát JSON: {ex.Message}");
            }
            catch (Exception ex)
            {
                await _logger.LogMessage("ERROR", $"Chyba při zpracování kurzů: {ex.Message}");
            }
        }

        public class ExchangeRateResponse
        {
            public bool Success { get; set; }
            public long Timestamp { get; set; }
            public string Source { get; set; } = string.Empty;
            public Dictionary<string, decimal> Quotes { get; set; } = new();
        }

        public class TimeframeResponse
        {
            public bool Success { get; set; }
            public bool Timeframe { get; set; }
            public string Start_Date { get; set; } = string.Empty;
            public string End_Date { get; set; } = string.Empty;
            public string Source { get; set; } = string.Empty;
            public Dictionary<string, Dictionary<string, decimal>> Quotes { get; set; } = new();
        }
    }
}