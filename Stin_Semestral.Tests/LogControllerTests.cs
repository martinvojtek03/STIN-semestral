using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Moq;
using Stin_Semestral.Controllers;
using Stin_Semestral.Data;
using Stin_Semestral.Models;
using System.Security.Claims;
using Xunit;

namespace Stin_Semestral.Tests
{
    public class LogControllerTests
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
        public async Task Index_ReturnsLatest100Logs_OrderedByTimestamp()
        {
            // --- ARRANGE ---
            using var context = GetInMemoryDbContext();

            // Naplníme DB testovacími daty (ExchangeLog)
            var now = DateTime.Now;
            for (int i = 1; i <= 105; i++)
            {
                context.Logs.Add(new ExchangeLog
                {
                    Id = i,
                    Level = i % 10 == 0 ? "Error" : "Info",
                    Message = $"Zpráva číslo {i}",
                    Timestamp = now.AddMinutes(i)
                });
            }
            await context.SaveChangesAsync();

            var controller = new LogController(context);
            SetupControllerContext(controller);

            // --- ACT ---
            var result = await controller.Index();

            // --- ASSERT ---
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<LogViewModel>>(viewResult.Model);

            // Ověření limitu 100 záznamů
            Assert.Equal(100, model.Count);

            // Ověření řazení (Index 0 musí být nejnovější, tedy ten s i=105)
            Assert.Equal("Zpráva číslo 105", model[0].Message);
            Assert.True(model[0].Timestamp > model[99].Timestamp);
        }

        [Fact]
        public async Task Index_ReturnsEmptyList_WhenDatabaseIsEmpty()
        {
            // --- ARRANGE ---
            using var context = GetInMemoryDbContext();
            var controller = new LogController(context);
            SetupControllerContext(controller);

            // --- ACT ---
            var result = await controller.Index();

            // --- ASSERT ---
            var viewResult = Assert.IsType<ViewResult>(result);
            var model = Assert.IsAssignableFrom<List<LogViewModel>>(viewResult.Model);
            Assert.Empty(model);
        }
    }
}