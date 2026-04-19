using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Stin_Semestral.Controllers;
using Stin_Semestral.Data;
using Stin_Semestral.Models;
using Stin_Semestral.Services;
using System.Security.Claims;
using Xunit;
using System.Net;
using System.Collections.Generic;
using System;
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

        private ExchangeRateService CreateActualService(ApplicationDbContext db, string jsonResponse = "{\"success\":true}")
        {
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["ExchangeRateApi:ApiKey"]).Returns("test-key");

            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock.Protected()
                .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
                .ReturnsAsync(new HttpResponseMessage { StatusCode = HttpStatusCode.OK, Content = new StringContent(jsonResponse) });

            var httpClient = new HttpClient(handlerMock.Object);
            var logger = new Logger(db);
            return new ExchangeRateService(db, httpClient, mockConfig.Object, logger);
        }

        [Fact]
        public async Task Index_CalculatesStatsCorrectly()
        {
            // Arrange
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

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            Assert.NotNull(controller.ViewData["Strongest"]);
            Assert.NotNull(controller.ViewData["Weakest"]);

            var strongest = controller.ViewData["Strongest"] as CurrencyViewModel;
            Assert.Equal("EUR", strongest?.Name);
        }

        [Fact]
        public async Task Index_WithNoData_ReturnsViewWithEmptyList()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new HomeController(context, CreateActualService(context));
            SetupControllerContext(controller);

            // Act
            var result = await controller.Index();

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<IEnumerable<CurrencyViewModel>>(viewResult.Model);
            Assert.Empty(model);
        }

        [Fact]
        public async Task UpdateRates_RedirectsToIndex()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var service = CreateActualService(context, "{\"success\":true}");
            var controller = new HomeController(context, service);
            SetupControllerContext(controller);

            // Act
            var result = await controller.UpdateRates();

            // Assert
            var redirect = Assert.IsType<RedirectToActionResult>(result);
            Assert.Equal("Index", redirect.ActionName);
        }

        [Theory]
        [InlineData(404, "Stránka, kterou hledáte, neexistuje.")]
        [InlineData(403, "Sem nemáte přístup.")]
        [InlineData(500, "Něco se pokazilo na naší straně.")]
        [InlineData(null, null)]
        public void Error_ReturnsCorrectMessagesForStatusCodes(int? code, string expectedMessagePart)
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var controller = new HomeController(context, CreateActualService(context));
            SetupControllerContext(controller);

            // Act
            var result = controller.Error(code);

            // Assert
            var viewResult = Assert.IsType<ViewResult>(result);
            if (code.HasValue)
            {
                var errorMessage = controller.ViewData["ErrorMessage"]?.ToString();
                Assert.NotNull(errorMessage);
                Assert.Contains(expectedMessagePart, errorMessage);
                Assert.Equal(code, controller.ViewData["StatusCode"]);
            }
        }
    }
}