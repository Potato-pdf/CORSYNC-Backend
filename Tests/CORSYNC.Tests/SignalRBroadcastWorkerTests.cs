using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using CORSYNC.Api.Hubs;
using CORSYNC.Api.Services;
using CORSYNC.Core.Domain;
using CORSYNC.Infrastructure.Telemetry;

namespace CORSYNC.Tests
{
    public class SignalRBroadcastWorkerTests
    {
        private readonly TelemetryChannel _channel;
        private readonly Mock<IHubContext<TelemetryHub>> _mockHubContext;
        private readonly Mock<IHubClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;
        private readonly Mock<ILogger<SignalRBroadcastWorker>> _mockLogger;
        private readonly SignalRBroadcastWorker _worker;

        public SignalRBroadcastWorkerTests()
        {
            _channel = new TelemetryChannel();
            _mockHubContext = new Mock<IHubContext<TelemetryHub>>();
            _mockClients = new Mock<IHubClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            _mockLogger = new Mock<ILogger<SignalRBroadcastWorker>>();

            // Setup HubContext mock
            _mockHubContext.Setup(h => h.Clients).Returns(_mockClients.Object);
            _mockClients.Setup(c => c.All).Returns(_mockClientProxy.Object);

            _worker = new SignalRBroadcastWorker(_channel, _mockHubContext.Object, _mockLogger.Object);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReadFromChannelAndBroadcastToAllClients()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            var reading = new LecturaCorazon
            {
                DispositivoId = "ESP32_Test",
                BPM = 72m,
                IR = 100000,
                BPMPromedio = 72
            };

            // Start the background worker
            var workerTask = _worker.StartAsync(cts.Token);

            // Act
            // Write a reading to the channel
            await _channel.Writer.WriteAsync(reading, cts.Token);

            // Wait a brief moment to allow the worker to process the message
            await Task.Delay(100);

            // Assert
            // Verify that Clients.All.SendAsync was called with the reading
            _mockClientProxy.Verify(
                p => p.SendCoreAsync(
                    "ReceiveTelemetry",
                    It.Is<object?[]>(args => args.Length == 1 && args[0] == reading),
                    It.IsAny<CancellationToken>()),
                Times.Once);

            // Clean up
            cts.Cancel();
            try
            {
                await workerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected when canceling host
            }
        }

        [Fact]
        public async Task ExecuteAsync_WhenChannelCloses_ShouldExitGracefully()
        {
            // Arrange
            using var cts = new CancellationTokenSource();
            
            // Start the background worker
            var workerTask = _worker.StartAsync(cts.Token);

            // Act
            // Close the channel writer
            _channel.Writer.Complete();

            // Wait for worker to finish (it should exit loop when channel is closed)
            await Task.WhenAny(workerTask, Task.Delay(2000));

            // Assert
            Assert.True(workerTask.IsCompleted);

            // Clean up
            cts.Cancel();
            try
            {
                await workerTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }
        }
    }
}
