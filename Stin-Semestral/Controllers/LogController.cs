using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stin_Semestral.Data;
using Stin_Semestral.Models;

namespace Stin_Semestral.Controllers
{
    [Authorize]
    public class LogController : Controller
    {
        private readonly ApplicationDbContext _context;

        public LogController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Načteme posledních 100 logů seřazených od nejnovějšího
            var logs = await _context.Logs
                .OrderByDescending(l => l.Timestamp)
                .Take(100)
                .Select(l => new LogViewModel
                {
                    Id = l.Id,
                    Timestamp = l.Timestamp,
                    Level = l.Level,
                    Message = l.Message
                })
                .ToListAsync();

            return View(logs);
        }
    }
}