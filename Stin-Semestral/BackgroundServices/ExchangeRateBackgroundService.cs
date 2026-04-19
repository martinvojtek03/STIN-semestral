using Stin_Semestral.Services;

namespace Stin_Semestral.BackgroundServices
{
    public class ExchangeRateBackgroundService : BackgroundService
    {
        private readonly IServiceProvider _services;
        private readonly Logger _logger;

        public ExchangeRateBackgroundService(IServiceProvider services, Logger logger)
        {
            _services = services;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            await _logger.LogMessage("INFO", "Background Service pro kurzy nastartována.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using (var scope = _services.CreateScope())
                    {
                        var exchangeService = scope.ServiceProvider.GetRequiredService<ExchangeRateService>();

                        await _logger.LogMessage("INFO", "Automatické stahování denních kurzů spuštěno...");

                        string json = await exchangeService.GetExchangeRatesAsync();
                        if (!string.IsNullOrEmpty(json))
                        {
                            await exchangeService.UpdateDatabaseRatesAsync(json);
                            await _logger.LogMessage("INFO", "Automatická aktualizace proběhla úspěšně.");
                        }
                    }
                }
                catch (Exception ex)
                {
                    await _logger.LogMessage("ERROR", $"Chyba v Background Service: {ex.Message}");
                }

                // Počkej 24 hodin (TimeSpan.FromHours(24))
                await Task.Delay(TimeSpan.FromHours(24), stoppingToken);
            }
        }
    }
}