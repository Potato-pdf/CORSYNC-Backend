using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CORSYNC.Core.Domain;
using CORSYNC.Core.Interfaces;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Infrastructure.Telemetry
{
    public class MqttTelemetryWorker : BackgroundService
    {
        private readonly ILogger<MqttTelemetryWorker> _logger;
        private readonly ITelemetryProcessor _processor;
        private readonly TelemetryChannel _channel;
        private readonly IServiceScopeFactory _scopeFactory;
        private readonly Random _random = new();

        public MqttTelemetryWorker(
            ILogger<MqttTelemetryWorker> logger,
            ITelemetryProcessor processor,
            TelemetryChannel channel,
            IServiceScopeFactory scopeFactory)
        {
            _logger = logger;
            _processor = processor;
            _channel = channel;
            _scopeFactory = scopeFactory;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogWarning("[PENDIENTE] Conexión real con el broker MQTT (HiveMQ Cloud, puerto 8883) marcada como PENDIENTE de configuración.");
            _logger.LogInformation("Iniciando bucle de simulación local de telemetría para el sensor MAX30102.");

            // Start the DB flushing loop (every 5 seconds)
            var dbFlushTask = RunDbFlushLoopAsync(stoppingToken);

            // Start the sensor reading simulator loop (generates readings at ~5Hz)
            var sensorSimulatorTask = RunSensorSimulatorLoopAsync(stoppingToken);

            await Task.WhenAll(dbFlushTask, sensorSimulatorTask);
        }

        private async Task RunSensorSimulatorLoopAsync(CancellationToken stoppingToken)
        {
            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Simulate raw payload from ESP32
                    // 90% chance of valid data, 10% chance of noise/outlier (sensor contact lost, movement artifact)
                    LecturaCorazon rawReading;
                    int roll = _random.Next(100);
                    if (roll < 5)
                    {
                        // Simulate outlier: extreme pulse rate (should be filtered out)
                        rawReading = new LecturaCorazon
                        {
                            DispositivoId = "ESP32_MAX30102_SIM",
                            BPM = _random.Next(10) == 0 ? 250m : 15m,
                            IR = 100000
                        };
                    }
                    else if (roll < 10)
                    {
                        // Simulate outlier: sensor contact lost (IR < 50000, should be filtered out)
                        rawReading = new LecturaCorazon
                        {
                            DispositivoId = "ESP32_MAX30102_SIM",
                            BPM = 75m,
                            IR = 25000
                        };
                    }
                    else
                    {
                        // Valid heart reading
                        decimal baseBpm = 70m + (decimal)(Math.Sin(DateTime.UtcNow.Ticks / 10000000.0) * 10.0); // sine wave pulse simulation
                        rawReading = new LecturaCorazon
                        {
                            DispositivoId = "ESP32_MAX30102_SIM",
                            BPM = baseBpm + (decimal)(_random.NextDouble() * 4.0 - 2.0),
                            IR = 102000 + _random.Next(-1000, 1000)
                        };
                    }

                    // Process and clean the reading
                    if (_processor.Validate(rawReading))
                    {
                        var smoothed = _processor.Smooth(rawReading);
                        
                        // Buffer it for database throttling
                        _processor.AddToBuffer(smoothed);

                        // Push immediately to the Channel for real-time SignalR streaming
                        await _channel.Writer.WriteAsync(smoothed, stoppingToken);
                    }
                    else
                    {
                        _logger.LogTrace("Lectura descartada por inconsistencia de sensor: BPM={Bpm}, IR={Ir}", rawReading.BPM, rawReading.IR);
                    }

                    // Sleep for 200ms (simulate 5Hz transmission rate)
                    await Task.Delay(200, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error en el bucle de simulación de sensor.");
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }

        private async Task RunDbFlushLoopAsync(CancellationToken stoppingToken)
        {
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
