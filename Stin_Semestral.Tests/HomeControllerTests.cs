using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Stin_Semestral.Controllers;
using Stin_Semestral.Data;
using Stin_Semestral.Models;
using Stin_Semestral.Services;
using System.Security.Claims;
using Xunit;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Stin_Semestral.Tests
{
    public class HomeControllerTests
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
            var modelMetadataProvider = new EmptyModelMetadataProvider();

            controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
            controller.ViewData = new ViewDataDictionary(modelMetadataProvider, new ModelStateDictionary());
            controller.TempData = new TempDataDictionary(httpContext, Mock.Of<ITempDataProvider>());
        }

        private ExchangeRateService CreateActualService(ApplicationDbContext db)
        {
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["ExchangeRateApi:ApiKey"]).Returns("test-key");
            var httpClient = new HttpClient();
            var logger = new Logger(db);
            return new ExchangeRateService(db, httpClient, mockConfig.Object, logger);
        }

        [Fact]
        public async Task Index_CalculatesStatsCorrectly()
        {
            using var context = GetInMemoryDbContext();
            context.Settings.Add(new UserSettings { BaseCurrency = "USD", SelectedCurrencies = "CZK,EUR" });
            var today = DateTime.Today;
            context.Currencies.AddRange(
                new Currency { Name = "USD", Rate = 1.0m, Date = today },
                new Currency { Name = "EUR", Rate = 0.9m, Date = today },
                new Currency { Name = "CZK", Rate = 23.0m, Date = today }
            );
            await context.SaveChangesAsync();

            var controller = new HomeController(context, CreateActualService(context));
            SetupControllerContext(controller);

            var result = await controller.Index();
            var viewResult = Assert.IsType<ViewResult>(result);

            var strongest = viewResult.ViewData["Strongest"] as CurrencyViewModel;
            Assert.NotNull(strongest);
            Assert.Equal("EUR", strongest.Name);
        }

        // --- NOVÝ TEST PRO BRANCH COVERAGE: Prázdná databáze ---
        [Fact]
        public async Task Index_WithNoData_ReturnsViewWithEmptyStats()
        {
            // Arrange - DB je úplně prázdná, žádné nastavení, žádné kurzy
            using var context = GetInMemoryDbContext();
            var controller = new HomeController(context, CreateActualService(context));
            SetupControllerContext(controller);

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            // Ověříme, že i při prázdné DB metoda nespadne a vrátí View
            Assert.Null(viewResult.ViewData["Strongest"]);
            Assert.Null(viewResult.ViewData["Weakest"]);
        }

        // --- NOVÝ TEST PRO BRANCH COVERAGE: Chybějící kurzy pro vybrané měny ---
        [Fact]
        public async Task Index_WithMissingRates_HandlesGracefully()
        {
            // Arrange - Máme nastavení, ale v DB nejsou kurzy pro ty konkrétní měny
            using var context = GetInMemoryDbContext();
            context.Settings.Add(new UserSettings { BaseCurrency = "USD", SelectedCurrencies = "NONEXISTENT" });
            await context.SaveChangesAsync();

            var controller = new HomeController(context, CreateActualService(context));
            SetupControllerContext(controller);

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Null(viewResult.ViewData["Strongest"]);
        }

        [Fact]
        public void Error_ReturnsCorrectViewData()
        {
            using var context = GetInMemoryDbContext();
            var controller = new HomeController(context, CreateActualService(context));
            SetupControllerContext(controller);

            var result = controller.Error(404);
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(404, viewResult.ViewData["StatusCode"]);
        }
    }
}