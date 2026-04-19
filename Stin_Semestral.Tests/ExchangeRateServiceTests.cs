using System.Net;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Moq.Protected;
using Stin_Semestral.Data;
using Stin_Semestral.Models;
using Stin_Semestral.Services;
using Xunit;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System;

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
            var service = new ExchangeRateService(context, httpClient, GetMockConfig().Object, new Logger(context));

            var result = await service.GetExchangeRatesAsync();

            Assert.Equal(jsonResponse, result);
        }

        [Fact]
        public async Task GetExchangeRatesAsync_ReturnsEmpty_WhenApiKeyMissing()
        {
            using var context = GetInMemoryDbContext();
            var service = new ExchangeRateService(context, new HttpClient(), GetMockConfig(null).Object, new Logger(context));

            var result = await service.GetExchangeRatesAsync();

            Assert.Empty(result);
        }

        [Fact]
        public async Task GetHistoricalRatesAsync_ReturnsEmpty_WhenApiKeyMissing()
        {
            using var context = GetInMemoryDbContext();
            var service = new ExchangeRateService(context, new HttpClient(), GetMockConfig(null).Object, new Logger(context));

            var result = await service.GetHistoricalRatesAsync(1);

            Assert.Empty(result);
        }

        [Fact]
        public async Task UpdateDatabaseTimeframeAsync_ApiKeyMissing_ReturnsImmediately()
        {
            using var context = GetInMemoryDbContext();
            var service = new ExchangeRateService(context, new HttpClient(), GetMockConfig(null).Object, new Logger(context));

            await service.UpdateDatabaseTimeframeAsync(1);

            Assert.Empty(await context.Currencies.ToListAsync());
        }

        [Fact]
        public async Task UpdateDatabaseRatesAsync_HandlesEdgeCaseCurrencyNames()
        {
            // Tento test pokrývá větve:
            // 1. StartsWith("USD") a Substring(3)
            // 2. Název měny co NEZAČÍNÁ na USD
            // 3. Prázdný název měny (přesměrování na "USD")
            using var context = GetInMemoryDbContext();
            var service = new ExchangeRateService(context, new HttpClient(), GetMockConfig().Object, new Logger(context));
            var json = @"{
                ""success"": true, 
                ""timestamp"": 1712150400, 
                ""quotes"": {
                    ""USDCZK"": 23.0, 
                    ""EUR"": 0.9,
                    ""USD"": 1.0
                }
            }";

            await service.UpdateDatabaseRatesAsync(json);

            var rates = await context.Currencies.ToListAsync();
            Assert.Contains(rates, r => r.Name == "CZK"); // Substring test
            Assert.Contains(rates, r => r.Name == "EUR"); // Else větev
            Assert.Contains(rates, r => r.Name == "USD"); // IsNullOrEmpty větev
        }

        [Fact]
        public async Task UpdateDatabaseTimeframeAsync_HandlesDifferentCurrencyFormats()
        {
            // Pokrývá logiku v cyklu timeframe, která je podobná UpdateDatabaseRates
            var timeframeJson = @"{
                ""success"": true,
                ""quotes"": {
                    ""2024-01-01"": { ""USDCZK"": 22.0, ""GBP"": 0.8 }
                }
            }";

            var httpClient = CreateMockHttpClient(timeframeJson);
            using var context = GetInMemoryDbContext();
            var service = new ExchangeRateService(context, httpClient, GetMockConfig().Object, new Logger(context));

            await service.UpdateDatabaseTimeframeAsync(1);

            var rates = await context.Currencies.ToListAsync();
            Assert.Contains(rates, r => r.Name == "CZK");
            Assert.Contains(rates, r => r.Name == "GBP");
        }

        [Fact]
        public async Task UpdateDatabaseTimeframeAsync_ExceptionInLoop_LogsError()
        {
            // Test pro obecný catch blok v Timeframe
            var httpClient = CreateFailingHttpClient();
            using var context = GetInMemoryDbContext();
            var service = new ExchangeRateService(context, httpClient, GetMockConfig().Object, new Logger(context));

            await service.UpdateDatabaseTimeframeAsync(1);

            var logs = await context.Logs.ToListAsync();
            Assert.Contains(logs, l => l.Level == "ERROR");
        }

        [Fact]
        public async Task UpdateDatabaseRatesAsync_ParsesJsonAndSavesToDb()
        {
            using var context = GetInMemoryDbContext();
            var service = new ExchangeRateService(context, new HttpClient(), GetMockConfig().Object, new Logger(context));
            var json = "{\"success\": true, \"timestamp\": 1712150400, \"quotes\": {\"USDCZK\": 23.5}}";

            await service.UpdateDatabaseRatesAsync(json);

            var rates = await context.Currencies.ToListAsync();
            Assert.NotEmpty(rates);
        }

        [Fact]
        public async Task UpdateDatabaseRatesAsync_HandlesInvalidJson()
        {
            using var context = GetInMemoryDbContext();
            var service = new ExchangeRateService(context, new HttpClient(), GetMockConfig().Object, new Logger(context));
            await service.UpdateDatabaseRatesAsync("invalid");

            var logs = await context.Logs.ToListAsync();
            Assert.Contains(logs, l => l.Message.Contains("Neplatný formát JSON"));
        }
    }
}