
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace SistemInformasiRestoran.Services
{
    public class ReservasiStokBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly ILogger<ReservasiStokBackgroundService> _logger;

        public ReservasiStokBackgroundService(
            IServiceScopeFactory scopeFactory,
            ILogger<ReservasiStokBackgroundService> logger)
        {
            _scopeFactory = scopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(
            CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    using var scope = _scopeFactory.CreateScope();

                    var service = scope.ServiceProvider
                        .GetRequiredService<ReservasiStokService>();

                    int jumlah = await service.KembalikanKedaluwarsaAsync();

                    if (jumlah > 0)
                    {
                        _logger.LogInformation(
                            "{Jumlah} reservasi stok kedaluwarsa telah diproses.",
                            jumlah);
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Terjadi kesalahan saat mengembalikan stok reservasi.");
                }

                await Task.Delay(
                    TimeSpan.FromMinutes(1),
                    stoppingToken);
            }
        }
    }
}
