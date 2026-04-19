using Microsoft.EntityFrameworkCore;
using Stin_Semestral.Data;
using Stin_Semestral.Models;
using Xunit;

namespace Stin_Semestral.Tests
{
    public class DbContextTests
    {
        private ApplicationDbContext GetContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        [Fact]
        public async Task Database_CanSaveAndRetrieveAllEntities()
        {
            // Arrange
            using var context = GetContext();

            // Act & Assert (Currencies)
            context.Currencies.Add(new Currency { Name = "USD", Rate = 1.0m, Date = DateTime.Now });

            // Act & Assert (Logs)
            context.Logs.Add(new ExchangeLog { Message = "Test Log", Level = "Info" });

            // Act & Assert (Settings)
            context.Settings.Add(new UserSettings { BaseCurrency = "CZK", SelectedCurrencies = "USD,EUR" });

            var saved = await context.SaveChangesAsync();

            // Assert
            Assert.Equal(3, saved);
            Assert.NotEmpty(await context.Currencies.ToListAsync());
            Assert.NotEmpty(await context.Logs.ToListAsync());
            Assert.NotEmpty(await context.Settings.ToListAsync());
        }
    }
}