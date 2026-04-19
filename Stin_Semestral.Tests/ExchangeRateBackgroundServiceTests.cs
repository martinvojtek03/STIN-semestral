using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Moq;
using Stin_Semestral.BackgroundServices;
using Stin_Semestral.Data;
using Stin_Semestral.Models;
using Stin_Semestral.Services;
using Xunit;
using Microsoft.Extensions.Configuration;

namespace Stin_Semestral.Tests
{
    public class ExchangeRateBackgroundServiceTests
    {
        [Fact]
        public async Task ExecuteAsync_RunsAndCallsServices_Correctly()
        {
            // --- ARRANGE ---
            var mockServiceProvider = new Mock<IServiceProvider>();
            var mockServiceScope = new Mock<IServiceScope>();
            var mockServiceScopeFactory = new Mock<IServiceScopeFactory>();

            // 1. DbContext (In-Memory)
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;

            using var context = new ApplicationDbContext(options);

            // 2. Závislosti pro ExchangeRateService
            var logger = new Logger(context);
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["ExchangeRateApi:ApiKey"]).Returns("test-key");

            // 3. Skutečná instance služby (nekoliduje s Moq omezeními)
            var actualExchangeService = new ExchangeRateService(
                context,
                new HttpClient(),
                mockConfig.Object,
                logger
            );

            // 4. Propojení ServiceProvideru pro BackgroundService
            mockServiceProvider.Setup(x => x.GetService(typeof(IServiceScopeFactory)))
                .Returns(mockServiceScopeFactory.Object);

            mockServiceScopeFactory.Setup(x => x.CreateScope())
                .Returns(mockServiceScope.Object);

            mockServiceScope.Setup(x => x.ServiceProvider)
                .Returns(mockServiceProvider.Object);

            // Registrace konkrétních typů, které si služba vytahuje přes GetRequiredService
            mockServiceProvider.Setup(x => x.GetService(typeof(Logger)))
                .Returns(logger);

            mockServiceProvider.Setup(x => x.GetService(typeof(ExchangeRateService)))
                .Returns(actualExchangeService);

            mockServiceProvider.Setup(x => x.GetService(typeof(ApplicationDbContext)))
                .Returns(context);

            var backgroundService = new ExchangeRateBackgroundService(mockServiceProvider.Object);

            // --- ACT ---
            using var cts = new CancellationTokenSource();

            // Spustíme BackgroundService
            var serviceTask = backgroundService.StartAsync(cts.Token);

            // Necháme proběhnout první cyklus
            await Task.Delay(500);

            // Zastavíme službu
            cts.Cancel();
            await backgroundService.StopAsync(CancellationToken.None);

            // --- ASSERT ---
            // Ověříme, že služba zapsala log do naší In-Memory DB
            var logs = await context.Logs.ToListAsync();
            Assert.NotEmpty(logs);
            Assert.Contains(logs, l => l.Message.Contains("Background Service pro kurzy nastartována"));
        }
    }
}