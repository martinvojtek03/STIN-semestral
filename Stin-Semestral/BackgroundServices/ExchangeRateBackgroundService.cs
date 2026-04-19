using Stin_Semestral.Data;
using Stin_Semestral.Services;
using Microsoft.EntityFrameworkCore;

namespace Stin_Semestral.BackgroundServices
{
    public class ExchangeRateBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _services;

        public ExchangeRateBackgroundService(IServiceProvider services)
        {
            _services = services;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            using (var scope = _services.CreateScope())
            {
                var logger = scope.ServiceProvider.GetRequiredService<Logger>();
                await logger.LogMessage("INFO", "Background Service pro kurzy nastartována.");
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _services.CreateScope())
                    {
                        var exchangeService = scope.ServiceProvider.GetRequiredService<ExchangeRateService>();
                        var logger = scope.ServiceProvider.GetRequiredService<Logger>();
                        var dbContext = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

                        await logger.LogMessage("INFO", "Automatické stahování denních kurzů spuštěno...");

                        string json = await exchangeService.GetExchangeRatesAsync();
                        if (!string.IsNullOrEmpty(json))
                        {
                            // 1. Aktualizace nových kurzů
                            await exchangeService.UpdateDatabaseRatesAsync(json);

                            // 2. Úklid starých dat (starší než 30 dní)
                            DateTime limitDate = DateTime.Today.AddDays(-30);
                            var oldRates = dbContext.Currencies.Where(c => c.Date < limitDate);

                            int deletedCount = await oldRates.ExecuteDeleteAsync(stoppingToken);

                            await logger.LogMessage("INFO", $"Automatická aktualizace proběhla úspěšně. Smazáno {deletedCount} starých záznamů.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    using (var scope = _services.CreateScope())
                    {
                        var logger = scope.ServiceProvider.GetRequiredService<Logger>();
                        await logger.LogMessage("ERROR", $"Chyba v Background Service: {ex.Message}");
                    }
                }

                // Počkej 24 hodin
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}