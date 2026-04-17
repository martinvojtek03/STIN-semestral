using System.Text.Json;
using Stin_Semestral.Data;
using Stin_Semestral.Models;
using Microsoft.Extensions.Configuration; // Musíš přidat pro přístup k tajnostem

namespace Stin_Semestral.Services
{
    public class ExchangeRateService
    {
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient;
        private readonly string _apiKey;

        // Konstruktor teď přijímá IConfiguration, aby mohl přečíst API klíč
        public ExchangeRateService(ApplicationDbContext context, HttpClient httpClient, IConfiguration configuration)
        {
            _context = context;
            _httpClient = httpClient;

            // Načte hodnotu z User Secrets nebo appsettings.json
            // Cesta odpovídá struktuře: ExchangeRateApi -> ApiKey
            _apiKey = configuration["ExchangeRateApi:ApiKey"] ?? "";
        }

        public async Task<string> GetExchangeRatesAsync(string baseCurrency = "EUR")
        {
            // Diagnostika: Vypíše do konzole, jestli se klíč podařilo načíst
            if (string.IsNullOrEmpty(_apiKey))
            {
                string errorMsg = "API klíč nebyl v konfiguraci nalezen!";
                Console.WriteLine($"CHYBA: {errorMsg}");
                await LogMessage("ERROR", errorMsg);
                return string.Empty;
            }

            try
            {
                // URL pro exchangerate.host API s použitím načteného klíče
                string url = $"https://api.exchangerate.host/live?access_key={_apiKey}&source=USD";

                var response = await _httpClient.GetStringAsync(url);

                await LogMessage("INFO", $"Úspěšně staženy kurzy pro {baseCurrency}.");

                return response;
            }
            catch (Exception ex)
            {
                await LogMessage("ERROR", $"Chyba při stahování kurzů: {ex.Message}");
                return string.Empty;
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
}