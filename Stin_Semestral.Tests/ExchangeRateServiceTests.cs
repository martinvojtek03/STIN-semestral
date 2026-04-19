using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Stin_Semestral.Data;
using Stin_Semestral.Models;
using Stin_Semestral.Services;
using Xunit;

namespace Stin_Semestral.Tests
{
    public class ExchangeRateServiceTests
    {
        private ApplicationDbContext GetInMemoryDbContext()
        {
            var options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
                .Options;
            return new ApplicationDbContext(options);
        }

        private Mock<IConfiguration> GetMockConfig(string apiKey = "test_key")
        {
            var mockConfig = new Mock<IConfiguration>();
            mockConfig.Setup(c => c["ExchangeRateApi:ApiKey"]).Returns(apiKey);
            return mockConfig;
        }

        private HttpClient CreateMockHttpClient(string responseContent, HttpStatusCode statusCode = HttpStatusCode.OK)
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ReturnsAsync(new HttpResponseMessage
               {
                   StatusCode = statusCode,
                   Content = new StringContent(responseContent),
               });

            return new HttpClient(handlerMock.Object);
        }

        [Fact]
        public async Task GetExchangeRatesAsync_ReturnsJson_WhenApiKeyExists()
        {
            // --- ARRANGE ---
            var jsonResponse = "{\"success\": true, \"quotes\": {\"USDCZK\": 23.5}}";
            var httpClient = CreateMockHttpClient(jsonResponse);
            using var context = GetInMemoryDbContext();
            var logger = new Logger(context);
            var service = new ExchangeRateService(context, httpClient, GetMockConfig().Object, logger);

            // --- ACT ---
            var result = await service.GetExchangeRatesAsync();

            // --- ASSERT ---
            Assert.Equal(jsonResponse, result);
        }

        [Fact]
        public async Task GetExchangeRatesAsync_ReturnsEmpty_AndLogsError_WhenApiKeyMissing()
        {
            // --- ARRANGE ---
            var httpClient = new HttpClient();
            using var context = GetInMemoryDbContext();
            var logger = new Logger(context);
            // Config vrátí null pro API Key
            var service = new ExchangeRateService(context, httpClient, GetMockConfig(null).Object, logger);

            // --- ACT ---
            var result = await service.GetExchangeRatesAsync();

            // --- ASSERT ---
            Assert.Empty(result);
            var logs = await context.Logs.ToListAsync();
            Assert.Contains(logs, l => l.Level == "ERROR" && l.Message.Contains("API klíč"));
        }

        [Fact]
        public async Task UpdateDatabaseRatesAsync_HandlesInvalidJson()
        {
            // Arrange
            using var context = GetInMemoryDbContext();
            var service = new ExchangeRateService(context, new HttpClient(), GetMockConfig().Object, new Logger(context));
            string invalidJson = "invalid json content"; // Tohle vyhodí výjimku při parsování

            // Act
            await service.UpdateDatabaseRatesAsync(invalidJson);

            // Assert
            // Ověříme, že v DB nic není a aplikace nespadla (vstoupilo to do catch bloku)
            Assert.Empty(await context.Currencies.ToListAsync());
        }


        [Fact]
        public async Task UpdateDatabaseRatesAsync_ParsesJsonAndSavesToDb()
        {
            // --- ARRANGE ---
            using var context = GetInMemoryDbContext();
            var logger = new Logger(context);
            var service = new ExchangeRateService(context, new HttpClient(), GetMockConfig().Object, logger);

            // Simulace JSONu z API (Timestamp 1712150400 = 2024-04-03)
            var json = "{\"success\": true, \"timestamp\": 1712150400, \"quotes\": {\"USDCZK\": 23.5, \"USDEUR\": 0.92}}";

            // --- ACT ---
            await service.UpdateDatabaseRatesAsync(json);

            // --- ASSERT ---
            var rates = await context.Currencies.ToListAsync();

            // Máme 3 záznamy: CZK, EUR a tvé ručně přidané USD
            Assert.Equal(3, rates.Count);

            // Kontrola uříznutí "USD"
            Assert.Contains(rates, r => r.Name == "CZK" && r.Rate == 23.5m);
            Assert.Contains(rates, r => r.Name == "USD" && r.Rate == 1.0m);
        }

        [Fact]
        public async Task UpdateDatabaseTimeframeAsync_SavesMultipleDays()
        {
            // --- ARRANGE ---
            var timeframeJson = @"{
                ""success"": true,
                ""quotes"": {
                    ""2024-01-01"": { ""USDCZK"": 22.0 },
                    ""2024-01-02"": { ""USDCZK"": 22.5 }
                }
            }";

            var httpClient = CreateMockHttpClient(timeframeJson);
            using var context = GetInMemoryDbContext();
            var logger = new Logger(context);
            var service = new ExchangeRateService(context, httpClient, GetMockConfig().Object, logger);

            // --- ACT ---
            await service.UpdateDatabaseTimeframeAsync(2);

            // --- ASSERT ---
            var rates = await context.Currencies.ToListAsync();

            // 2 dny * (CZK + ruční USD) = 4 záznamy
            Assert.Equal(4, rates.Count);
            Assert.Contains(rates, r => r.Date == new DateTime(2024, 1, 1) && r.Name == "CZK");
            Assert.Contains(rates, r => r.Date == new DateTime(2024, 1, 2) && r.Name == "CZK");
        }
    }
}