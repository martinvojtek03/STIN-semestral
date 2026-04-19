using Microsoft.EntityFrameworkCore;
using Stin_Semestral.Data;
using Stin_Semestral.Models;
using Stin_Semestral.Services;
using Xunit;

namespace Stin_Semestral.Tests
{
    public class LoggerTests
    {
        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task LogMessage_SavesToDatabase_WithCorrectData()
        {
            // --- ARRANGE ---
            using var context = GetInMemoryDbContext();
            var logger = new Logger(context);
            string testLevel = "ERROR";
            string testMessage = "Kritická chyba systému";

            // --- ACT ---
            await logger.LogMessage(testLevel, testMessage);

            // --- ASSERT ---
            // Ověříme, že v tabulce Logs je přesně jeden záznam
            var logCount = await context.Logs.CountAsync();
            Assert.Equal(1, logCount);

            // Vytáhneme si ten záznam a zkontrolujeme data
            var logEntry = await context.Logs.FirstOrDefaultAsync();
            Assert.NotNull(logEntry);
            Assert.Equal(testLevel, logEntry.Level);
            Assert.Equal(testMessage, logEntry.Message);

            // Ověříme, že Timestamp byl nastaven na aktuální čas (s tolerancí pár sekund)
            Assert.True((DateTime.Now - logEntry.Timestamp).TotalSeconds < 5);
        }

        [Fact]
        public async Task LogMessage_CanSaveMultipleLogs()
        {
            // --- ARRANGE ---
            using var context = GetInMemoryDbContext();
            var logger = new Logger(context);

            // --- ACT ---
            await logger.LogMessage("INFO", "První zpráva");
            await logger.LogMessage("WARNING", "Druhá zpráva");
            await logger.LogMessage("ERROR", "Třetí zpráva");

            // --- ASSERT ---
            var logs = await context.Logs.ToListAsync();
            Assert.Equal(3, logs.Count);
            Assert.Equal("INFO", logs[0].Level);
            Assert.Equal("WARNING", logs[1].Level);
            Assert.Equal("ERROR", logs[2].Level);
        }
    }
}