using Stin_Semestral.Data;
using Stin_Semestral.Models;

namespace Stin_Semestral.Services
{
    public class ExchangeRateService
    {
        private readonly ApplicationDbContext _context;
        private readonly HttpClient _httpClient;

        public ExchangeRateService(ApplicationDbContext context, HttpClient httpClient)
        {
            _context = context;
            _httpClient = httpClient;
        }

        // Metoda pro uložení logu do databáze
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