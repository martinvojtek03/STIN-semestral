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

        // Pomocná metoda pro vytvoření skutečné služby s namockovanými závislostmi
        private ExchangeRateService CreateActualService(ApplicationDbContext db)
        {
            var mockConfig = new Mock<IConfiguration>();
            // Nastavíme prázdný API klíč, aby konstruktor služby neselhal
            mockConfig.Setup(c => c["ExchangeRateApi:ApiKey"]).Returns("test-key");

            var httpClient = new HttpClient();
            var logger = new Logger(db); // Skutečný logger používající naši in-memory DB

            return new ExchangeRateService(db, httpClient, mockConfig.Object, logger);
        }

        [Fact]
        public async Task Index_CalculatesStatsCorrectly()
        {
            // --- ARRANGE ---
            using var context = GetInMemoryDbContext();

            // Příprava dat
            context.Settings.Add(new UserSettings { BaseCurrency = "USD", SelectedCurrencies = "CZK,EUR" });
            var today = DateTime.Today;
            context.Currencies.AddRange(
                new Currency { Name = "USD", Rate = 1.0m, Date = today },
                new Currency { Name = "EUR", Rate = 0.9m, Date = today },
                new Currency { Name = "CZK", Rate = 23.0m, Date = today }
            );
            await context.SaveChangesAsync();

            // Vytvoříme SKUTEČNOU instanci služby, ne Mock
            var actualService = CreateActualService(context);
            var controller = new HomeController(context, actualService);
            SetupControllerContext(controller);

            // --- ACT ---
            var result = await controller.Index();

            // --- ASSERT ---
            var viewResult = Assert.IsType<ViewResult>(result);

            var strongest = viewResult.ViewData["Strongest"] as CurrencyViewModel;
            var weakest = viewResult.ViewData["Weakest"] as CurrencyViewModel;

            Assert.NotNull(strongest);
            Assert.Equal("EUR", strongest.Name);
            Assert.Equal("CZK", weakest?.Name);
        }

        [Fact]
        public void Error_ReturnsCorrectViewData()
        {
            // --- ARRANGE ---
            using var context = GetInMemoryDbContext();
            var controller = new HomeController(context, CreateActualService(context));
            SetupControllerContext(controller);

            // --- ACT ---
            var result = controller.Error(404);

            // --- ASSERT ---
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.Equal(404, viewResult.ViewData["StatusCode"]);
            Assert.Contains("neexistuje", viewResult.ViewData["ErrorMessage"]?.ToString());
        }
    }
}