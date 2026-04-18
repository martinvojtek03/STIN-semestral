using Microsoft.EntityFrameworkCore;
using Stin_Semestral.Models;
using Stin_Semestral.Data;

namespace Stin_Semestral.Services
{
    public class Logger
    {
        private readonly ApplicationDbContext _context;
        public Logger(ApplicationDbContext context)
        {
            _context = context;
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
