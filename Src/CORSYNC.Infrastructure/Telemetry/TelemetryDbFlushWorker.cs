using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CORSYNC.Core.Interfaces;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Infrastructure.Telemetry
{
    public class TelemetryDbFlushWorker : BackgroundService
    {
        private readonly ILogger<TelemetryDbFlushWorker> _logger;
        private readonly ITelemetryProcessor _processor;
        private readonly IServiceScopeFactory _scopeFactory;

        public TelemetryDbFlushWorker(
            ILogger<TelemetryDbFlushWorker> logger,
            ITelemetryProcessor processor,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _processor = processor;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Iniciando servicio en segundo plano para persistencia de telemetría promediada (Throttling DB).");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Flush every 5 seconds
                    await Task.Delay(5000, stoppingToken);

                    var consolidated = _processor.FlushBuffer();
                    if (consolidated != null)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        var dbContext = scope.ServiceProvider.GetRequiredService<TelemetryDbContext>();

                        dbContext.LecturasCorazon.Add(consolidated);
                        await dbContext.SaveChangesAsync(stoppingToken);

                        _logger.LogInformation(
                            "[Throttling] Guardado lote consolidado en BD: BPM Promedio = {BpmProm}, Registros promediados. Dispositivo: {DispId}", 
                            consolidated.BPMPromedio, consolidated.DispositivoId);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al persistir telemetría promediada.");
                }
            }
        }
    }
}
