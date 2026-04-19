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

        private HttpClient CreateFailingHttpClient()
        {
            var handlerMock = new Mock<HttpMessageHandler>();
            handlerMock
               .Protected()
               .Setup<Task<HttpResponseMessage>>(
                  "SendAsync",
                  ItExpr.IsAny<HttpRequestMessage>(),
                  ItExpr.IsAny<CancellationToken>()
               )
               .ThrowsAsync(new HttpRequestException("Network error"));

            return new HttpClient(handlerMock.Object);
        }

        [Fact]
        public async Task GetExchangeRatesAsync_ReturnsJson_WhenApiKeyExists()
        {
            var jsonResponse = "{\"success\": true, \"quotes\": {\"USDCZK\": 23.5}}";
            var httpClient = CreateMockHttpClient(jsonResponse);
            using var context = GetInMemoryDbContext();
            var logger = new Logger(context);
            var service = new ExchangeRateService(context, httpClient, GetMockConfig().Object, logger);

            var result = await service.GetExchangeRatesAsync();

            Assert.Equal(jsonResponse, result);
        }

        [Fact]
        public async Task GetExchangeRatesAsync_ReturnsEmpty_AndLogsError_WhenApiKeyMissing()
        {
            var httpClient = new HttpClient();
            using var context = GetInMemoryDbContext();
            var logger = new Logger(context);
            var service = new ExchangeRateService(context, httpClient, GetMockConfig(null).Object, logger);

            var result = await service.GetExchangeRatesAsync();

            Assert.Empty(result);
            var logs = await context.Logs.ToListAsync();
            Assert.Contains(logs, l => l.Level == "ERROR" && l.Message.Contains("API klíč"));
        }

        // --- NOVÝ TEST: Pokrytí catch bloku v GetExchangeRatesAsync ---
        [Fact]
        public async Task GetExchangeRatesAsync_ReturnsEmpty_WhenExceptionOccurs()
        {
            var httpClient = CreateFailingHttpClient();
            using var context = GetInMemoryDbContext();
            var service = new ExchangeRateService(context, httpClient, GetMockConfig().Object, new Logger(context));

            var result = await service.GetExchangeRatesAsync();

            Assert.Empty(result);
            var logs = await context.Logs.ToListAsync();
            Assert.Contains(logs, l => l.Level == "ERROR" && l.Message.Contains("Chyba při stahování"));
        }

        [Fact]
        public async Task UpdateDatabaseRatesAsync_HandlesInvalidJson()
        {
            using var context = GetInMemoryDbContext();
            var service = new ExchangeRateService(context, new HttpClient(), GetMockConfig().Object, new Logger(context));
            string invalidJson = "invalid json content";

            await service.UpdateDatabaseRatesAsync(invalidJson);

            Assert.Empty(await context.Currencies.ToListAsync());
        }

        // --- NOVÝ TEST: Pokrytí větve if (data != null && data.Success) ---
        [Fact]
        public async Task UpdateDatabaseRatesAsync_DoesNothing_WhenSuccessIsFalse()
        {
            using var context = GetInMemoryDbContext();
            var service = new ExchangeRateService(context, new HttpClient(), GetMockConfig().Object, new Logger(context));
            string json = "{\"success\": false}";

            await service.UpdateDatabaseRatesAsync(json);

            Assert.Empty(await context.Currencies.ToListAsync());
        }

        // --- NOVÝ TEST: Pokrytí větve if (data.Quotes != null) ---
        [Fact]
        public async Task UpdateDatabaseRatesAsync_HandlesNullQuotes()
        {
            using var context = GetInMemoryDbContext();
            var service = new ExchangeRateService(context, new HttpClient(), GetMockConfig().Object, new Logger(context));
            string json = "{\"success\": true, \"timestamp\": 1712150400, \"quotes\": null}";

            await service.UpdateDatabaseRatesAsync(json);

            Assert.Empty(await context.Currencies.ToListAsync());
        }

        [Fact]
        public async Task UpdateDatabaseRatesAsync_ParsesJsonAndSavesToDb()
        {
            using var context = GetInMemoryDbContext();
            var logger = new Logger(context);
            var service = new ExchangeRateService(context, new HttpClient(), GetMockConfig().Object, logger);
            var json = "{\"success\": true, \"timestamp\": 1712150400, \"quotes\": {\"USDCZK\": 23.5, \"USDEUR\": 0.92}}";

            await service.UpdateDatabaseRatesAsync(json);

            var rates = await context.Currencies.ToListAsync();
            Assert.Equal(3, rates.Count);
            Assert.Contains(rates, r => r.Name == "CZK" && r.Rate == 23.5m);
            Assert.Contains(rates, r => r.Name == "USD" && r.Rate == 1.0m);
        }

        [Fact]
        public async Task UpdateDatabaseTimeframeAsync_SavesMultipleDays()
        {
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

            await service.UpdateDatabaseTimeframeAsync(2);

            var rates = await context.Currencies.ToListAsync();
            Assert.Equal(4, rates.Count);
        }

        // --- NOVÝ TEST: Pokrytí catch bloku v UpdateDatabaseTimeframeAsync ---
        [Fact]
        public async Task UpdateDatabaseTimeframeAsync_HandlesInvalidJson()
        {
            var httpClient = CreateMockHttpClient("invalid-json");
            using var context = GetInMemoryDbContext();
            var service = new ExchangeRateService(context, httpClient, GetMockConfig().Object, new Logger(context));

            await service.UpdateDatabaseTimeframeAsync(1);

            var logs = await context.Logs.ToListAsync();
            Assert.Contains(logs, l => l.Level == "ERROR" && l.Message.Contains("Neplatný JSON formát"));
        }

        // --- NOVÝ TEST: Pokrytí catch bloku v GetHistoricalRatesAsync ---
        [Fact]
        public async Task GetHistoricalRatesAsync_HandlesException()
        {
            var httpClient = CreateFailingHttpClient();
            using var context = GetInMemoryDbContext();
            var service = new ExchangeRateService(context, httpClient, GetMockConfig().Object, new Logger(context));

            var result = await service.GetHistoricalRatesAsync(1);

            Assert.Empty(result);
            var logs = await context.Logs.ToListAsync();
            Assert.Contains(logs, l => l.Level == "ERROR" && l.Message.Contains("Chyba při stahování historických dat"));
        }
    }
}