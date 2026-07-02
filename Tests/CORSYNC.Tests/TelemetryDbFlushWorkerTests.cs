using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;
using Xunit.Abstractions;
using CORSYNC.Core.Domain;
using CORSYNC.Core.Interfaces;
using CORSYNC.Infrastructure.Database;
using CORSYNC.Infrastructure.Telemetry;
using Microsoft.EntityFrameworkCore;

namespace CORSYNC.Tests
{
    public class TelemetryDbFlushWorkerTests
    {
        private readonly ITestOutputHelper _output;

        public TelemetryDbFlushWorkerTests(ITestOutputHelper output)
        {
            _output = output;
        }

        [Fact]
        public async Task Worker_FlushesBufferAndSavesToDatabase()
        {
            // Arrange
            var logger = new ExceptionThrowingLogger<TelemetryDbFlushWorker>();
            var mockProcessor = new Mock<ITelemetryProcessor>();
            
            // Set up a consolidated reading to be flushed
            var consolidatedReading = new LecturaCorazon
            {
                DispositivoId = "ESP32_Worker_Test",
                BPM = 72.5m,
                IR = 87432,
                BPMPromedio = 71,
                GsrRaw = 1340,
                GsrVoltaje = 1.079m,
                Aura = "Rojo",
                FechaHora = DateTime.UtcNow
            };

            // Setup mock processor to return consolidated reading on first call, then null
            bool calledOnce = false;
            mockProcessor.Setup(p => p.FlushBuffer())
                .Returns(() => {
                    if (!calledOnce)
                    {
                        calledOnce = true;
                        return consolidatedReading;
                    }
                    return null;
                });

            // Set up InMemory database
            var options = new DbContextOptionsBuilder<TelemetryDbContext>()
                .UseInMemoryDatabase(databaseName: $"CORSYNC_Telemetry_Worker_Test_{Guid.NewGuid()}")
                .Options;

            // Mock IServiceScopeFactory and IServiceScope
            var mockScope = new Mock<IServiceScope>();
            var mockServiceProvider = new Mock<IServiceProvider>();
            
            // Retornar una nueva instancia cada vez para evitar problemas de concurrencia de DbContext
            mockServiceProvider.Setup(x => x.GetService(typeof(TelemetryDbContext)))
                .Returns(() => new TelemetryDbContext(options));
            mockScope.Setup(x => x.ServiceProvider).Returns(mockServiceProvider.Object);
            
            var mockScopeFactory = new Mock<IServiceScopeFactory>();
            mockScopeFactory.Setup(x => x.CreateScope()).Returns(mockScope.Object);

            // Instantiate worker with a 10ms interval for fast test execution
            var worker = new ExposedTelemetryDbFlushWorker(logger, mockProcessor.Object, mockScopeFactory.Object, 10);

            // Act: Start worker
            using var cts = new CancellationTokenSource();
            var runTask = worker.CallExecuteAsync(cts.Token);

            // Wait until the database has the record, up to 2 seconds, to prevent race conditions
            int elapsed = 0;
            while (elapsed < 2000)
            {
                using (var checkContext = new TelemetryDbContext(options))
                {
                    if (await checkContext.LecturasCorazon.AnyAsync())
                    {
                        break;
                    }
                }
                await Task.Delay(10);
                elapsed += 10;
            }
            
            // Stop the worker
            cts.Cancel();

            try
            {
                await runTask;
            }
            catch (OperationCanceledException)
            {
                // Expected
            }

            // Assert
            // Verify processor was called
            mockProcessor.Verify(p => p.FlushBuffer(), Times.AtLeastOnce());

            // Verify that the consolidated reading was added and saved to the db Context using a fresh context
            using var verifyContext = new TelemetryDbContext(options);
            var readings = await verifyContext.LecturasCorazon.ToListAsync();
            Assert.NotEmpty(readings);
            Assert.Equal("ESP32_Worker_Test", readings[0].DispositivoId);
            Assert.Equal(72.5m, readings[0].BPM);
            Assert.Equal(87432L, readings[0].IR);
            Assert.Equal("Rojo", readings[0].Aura);
        }

        public class ExposedTelemetryDbFlushWorker : TelemetryDbFlushWorker
        {
            public ExposedTelemetryDbFlushWorker(ILogger<TelemetryDbFlushWorker> logger, ITelemetryProcessor processor, IServiceScopeFactory scopeFactory, int intervalMs)
                : base(logger, processor, scopeFactory, intervalMs)
            {
            }

            public Task CallExecuteAsync(CancellationToken stoppingToken)
            {
                return ExecuteAsync(stoppingToken);
            }
        }

        private class ExceptionThrowingLogger<T> : ILogger<T>
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (exception != null)
                {
                    throw new InvalidOperationException($"Worker Exception: {exception.Message}", exception);
                }
            }
        }
    }
}
