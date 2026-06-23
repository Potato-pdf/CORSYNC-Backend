using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using CORSYNC.Api.Hubs;
using CORSYNC.Infrastructure.Telemetry;

namespace CORSYNC.Api.Services
{
    public class SignalRBroadcastWorker : BackgroundService
    {
        private readonly TelemetryChannel _channel;
        private readonly IHubContext<TelemetryHub> _hubContext;
        private readonly ILogger<SignalRBroadcastWorker> _logger;

        public SignalRBroadcastWorker(
            TelemetryChannel channel,
            IHubContext<TelemetryHub> hubContext,
            ILogger<SignalRBroadcastWorker> logger)
        {
            _channel = channel;
            _hubContext = hubContext;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Iniciando servicio de retransmisión SignalR para telemetría.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    // Read from channel as it becomes available
                    var reading = await _channel.Reader.ReadAsync(stoppingToken);

                    // Broadcast to all clients
                    await _hubContext.Clients.All.SendAsync("ReceiveTelemetry", reading, cancellationToken: stoppingToken);
                }
                catch (System.Threading.Channels.ChannelClosedException)
                {
                    break;
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error al retransmitir telemetría vía SignalR.");
                }
            }
        }
    }
}
