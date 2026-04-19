using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Stin_Semestral.Models;
using Xunit;

namespace Stin_Semestral.Tests
{
    public class ModelTests
    {
        [Fact]
        public void UserSettings_Properties_And_Defaults_Work()
        {
            var settings = new UserSettings();

            Assert.Equal("USD", settings.BaseCurrency);
            Assert.Equal("CZK,EUR", settings.SelectedCurrencies);

            settings.Id = 10;
            settings.BaseCurrency = "EUR";
            settings.SelectedCurrencies = "USD";

            Assert.Equal(10, settings.Id);
            Assert.Equal("EUR", settings.BaseCurrency);
            Assert.Equal("USD", settings.SelectedCurrencies);
        }

        [Fact]
        public void Currency_Properties_Work()
        {
            var today = DateTime.Today;
            var currency = new Currency
            {
                Id = 1,
                Name = "CZK",
                Rate = 23.5m, // Přípona 'm' pro decimal
                Date = today
            };

            Assert.Equal(1, currency.Id);
            Assert.Equal("CZK", currency.Name);
            Assert.Equal(23.5m, currency.Rate);
            Assert.Equal(today, currency.Date);
        }

        [Fact]
        public void ExchangeLog_Defaults_And_Properties_Work()
        {
            var log = new ExchangeLog();

            Assert.Equal("Info", log.Level);
            // Tolerance pro běh testu
            Assert.True((DateTime.Now - log.Timestamp).TotalSeconds < 10);
            Assert.Empty(log.Message);

            log.Id = 5;
            log.Level = "Error";
            log.Message = "Testovací chyba";

            Assert.Equal(5, log.Id);
            Assert.Equal("Error", log.Level);
            Assert.Equal("Testovací chyba", log.Message);
        }

        [Fact]
        public void CurrencyViewModel_Properties_Work()
        {
            var today = DateTime.Now;
            var vm = new CurrencyViewModel
            {
                Name = "USD",
                CurrentRate = 1.0m,    // Přípona 'm'
                Average30Days = 0.95m, // OPRAVA: Podle tvého souboru je to decimal, tedy 'm'
                Date = today,          // OPRAVA: Musí být DateTime objekt
                HistoryJson = "[1.0, 1.1]"
            };

            Assert.Equal("USD", vm.Name);
            Assert.Equal(1.0m, vm.CurrentRate);
            Assert.Equal(0.95m, vm.Average30Days);
            Assert.Equal(today, vm.Date);
            Assert.Equal("[1.0, 1.1]", vm.HistoryJson);
        }

        [Fact]
        public void LogViewModel_Properties_Work()
        {
            var now = DateTime.Now;
            var vm = new LogViewModel
            {
                Id = 1,
                Timestamp = now,
                Level = "WARNING",
                Message = "Varovná zpráva"
            };

            Assert.Equal(1, vm.Id);
            Assert.Equal(now, vm.Timestamp);
            Assert.Equal("WARNING", vm.Level);
            Assert.Equal("Varovná zpráva", vm.Message);
        }

        [Fact]
        public void Model_Validation_Should_Catch_Missing_Required_Fields()
        {
            // Vytvoříme objekt, který porušuje [Required] u Name
            var currency = new Currency
            {
                Name = null!,
                Date = DateTime.Now,
                Rate = 1.0m
            };

            var context = new ValidationContext(currency);
            var results = new List<ValidationResult>();

            // Spustí validaci DataAnnotations
            var isValid = Validator.TryValidateObject(currency, context, results, true);

            Assert.False(isValid);
            Assert.Contains(results, r => r.MemberNames.Contains("Name"));
        }
    }
}