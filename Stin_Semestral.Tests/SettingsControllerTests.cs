using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Stin_Semestral.Controllers;
using Stin_Semestral.Data;
using Stin_Semestral.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Xunit;

namespace Stin_Semestral.Tests
{
    public class SettingsControllerTests
    {
        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private void SetupControllerContext(Controller controller)
        {
            var user = new ClaimsPrincipal(new ClaimsIdentity(new Claim[] {
                new Claim(ClaimTypes.Name, "admin")
            }, "mock"));

            var httpContext = new DefaultHttpContext { User = user };

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.ViewData = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary());
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        }

        [Fact]
        public async Task Index_ReturnsSettingsAndAllAvailableCurrencyNames()
        {
            // --- ARRANGE ---
            using var context = GetInMemoryDbContext();

            context.Currencies.AddRange(
                new Currency { Name = "USD", Rate = 1.0m, Date = DateTime.Today },
                new Currency { Name = "EUR", Rate = 0.9m, Date = DateTime.Today },
                new Currency { Name = "CZK", Rate = 23.0m, Date = DateTime.Today },
                new Currency { Name = "USD", Rate = 1.0m, Date = DateTime.Today.AddDays(-1) }
            );

            context.Settings.Add(new UserSettings { BaseCurrency = "EUR", SelectedCurrencies = "USD,CZK" });
            await context.SaveChangesAsync();

            var controller = new SettingsController(context);
            SetupControllerContext(controller);

            // --- ACT ---
            var result = await controller.Index();

            // --- ASSERT ---
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<UserSettings>(viewResult.Model);

            Assert.Equal("EUR", model.BaseCurrency);

            var allCurrencies = viewResult.ViewData["AllCurrencies"] as List<string>;
            Assert.NotNull(allCurrencies);
            Assert.Equal(3, allCurrencies.Count);
            Assert.Equal("CZK", allCurrencies[0]);
            Assert.Equal("USD", allCurrencies[2]);
        }

        // --- NOVÝ TEST: Pokrytí větve if (settings == null) v Index ---
        [Fact]
        public async Task Index_CreatesDefaultSettings_IfNoneExist()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new SettingsController(context);
            SetupControllerContext(controller);

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsType<UserSettings>(viewResult.Model);
            Assert.Equal("USD", model.BaseCurrency); // Výchozí hodnota z kódu
            Assert.Equal("CZK,EUR", model.SelectedCurrencies); // Výchozí hodnota z kódu
        }

        [Fact]
        public async Task Save_UpdatesExistingSettingsAndRedirectsToHome()
        {
            // --- ARRANGE ---
            using var context = GetInMemoryDbContext();
            context.Settings.Add(new UserSettings { BaseCurrency = "USD", SelectedCurrencies = "CZK" });
            await context.SaveChangesAsync();

            var controller = new SettingsController(context);
            SetupControllerContext(controller);

            var newSelected = new List<string> { "EUR", "GBP", "CZK" };

            // --- ACT ---
            var result = await controller.Save("EUR", newSelected);

            // --- ASSERT ---
            var redirectResult = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirectResult.ActionName);
            Assert.Equal("Home", redirectResult.ControllerName);

            var updatedSettings = await context.Settings.FirstOrDefaultAsync();
            Assert.NotNull(updatedSettings);
            Assert.Equal("EUR", updatedSettings.BaseCurrency);
            Assert.Equal("EUR,GBP,CZK", updatedSettings.SelectedCurrencies);
        }

        [Fact]
        public async Task Save_CreatesNewSettings_IfNoneExist()
        {
            // --- ARRANGE ---
            using var context = GetInMemoryDbContext();
            var controller = new SettingsController(context);
            SetupControllerContext(controller);

            // --- ACT ---
            await controller.Save("USD", new List<string> { "CZK" });

            // --- ASSERT ---
            var createdSettings = await context.Settings.FirstOrDefaultAsync();
            Assert.NotNull(createdSettings);
            Assert.Equal("USD", createdSettings.BaseCurrency);
            Assert.Equal("CZK", createdSettings.SelectedCurrencies);
        }

        // --- NOVÝ TEST: Pokrytí větve pro null SelectedCurrencies ---
        [Fact]
        public async Task Save_HandlesNullSelectedCurrencies()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new SettingsController(context);
            SetupControllerContext(controller);

            // Act
            await controller.Save("USD", null);

            // Assert
            var settings = await context.Settings.FirstOrDefaultAsync();
            Assert.NotNull(settings);
            Assert.Equal("", settings.SelectedCurrencies); // Mělo by uložit prázdný řetězec
        }
    }
}