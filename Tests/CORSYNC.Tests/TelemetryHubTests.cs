using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using CORSYNC.Api.Hubs;
using CORSYNC.Core.Domain;
using CORSYNC.Core.Interfaces;

namespace CORSYNC.Tests
{
    public class TelemetryHubTests
    {
        private readonly Mock<ILogger<TelemetryHub>> _mockLogger;
        private readonly Mock<ITelemetryProcessor> _mockProcessor;
        private readonly Mock<IHubCallerClients> _mockClients;
        private readonly Mock<IClientProxy> _mockClientProxy;
        private readonly Mock<IGroupManager> _mockGroups;
        private readonly Mock<HubCallerContext> _mockContext;
        private readonly TelemetryHub _hub;

        public TelemetryHubTests()
        {
            _mockLogger = new Mock<ILogger<TelemetryHub>>();
            _mockProcessor = new Mock<ITelemetryProcessor>();
            _mockClients = new Mock<IHubCallerClients>();
            _mockClientProxy = new Mock<IClientProxy>();
            _mockGroups = new Mock<IGroupManager>();
            _mockContext = new Mock<HubCallerContext>();

            // Setup Clients.Group to return our mockClientProxy
            _mockClients.Setup(c => c.Group(It.IsAny<string>())).Returns(_mockClientProxy.Object);

            // Setup HubCallerContext ConnectionId
            _mockContext.Setup(c => c.ConnectionId).Returns("conn-id-123");

            // Instantiate TelemetryHub with mocks
            _hub = new TelemetryHub(_mockLogger.Object, _mockProcessor.Object)
            {
                Clients = _mockClients.Object,
                Groups = _mockGroups.Object,
                Context = _mockContext.Object
            };
        }

        [Fact]
        public async Task RegisterDevice_WithValidId_ShouldAddConnectionToDeviceGroup()
        {
            // Arrange
            string deviceId = "ESP32_123";
            string expectedGroup = $"device_{deviceId}";

            // Act
            await _hub.RegisterDevice(deviceId);

            // Assert
            _mockGroups.Verify(
                g => g.AddToGroupAsync("conn-id-123", expectedGroup, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task RegisterDevice_WithNullOrEmptyId_ShouldNotAddToGroup(string? deviceId)
        {
            // Act
            await _hub.RegisterDevice(deviceId!);

            // Assert
            _mockGroups.Verify(
                g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task RegisterMobile_WithValidId_ShouldAddConnectionToMobileGroup()
        {
            // Arrange
            string deviceId = "ESP32_123";
            string expectedGroup = $"mobile_{deviceId}";

            // Act
            await _hub.RegisterMobile(deviceId);

            // Assert
            _mockGroups.Verify(
                g => g.AddToGroupAsync("conn-id-123", expectedGroup, It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task RegisterMobile_WithNullOrEmptyId_ShouldNotAddToGroup(string? deviceId)
        {
            // Act
            await _hub.RegisterMobile(deviceId!);

            // Assert
            _mockGroups.Verify(
                g => g.AddToGroupAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task StartMeasurement_WithValidId_ShouldSendStartTelemetryToDeviceGroup()
        {
            // Arrange
            string deviceId = "ESP32_123";
            string expectedGroup = $"device_{deviceId}";

            // Act
            await _hub.StartMeasurement(deviceId);

            // Assert
            _mockClients.Verify(c => c.Group(expectedGroup), Times.Once);
            _mockClientProxy.Verify(
                p => p.SendCoreAsync("StartTelemetry", It.Is<object?[]>(args => args.Length == 0), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task StartMeasurement_WithNullOrEmptyId_ShouldDoNothing(string? deviceId)
        {
            // Act
            await _hub.StartMeasurement(deviceId!);

            // Assert
            _mockClients.Verify(c => c.Group(It.IsAny<string>()), Times.Never);
            _mockClientProxy.Verify(
                p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task StopMeasurement_WithValidId_ShouldSendStopTelemetryToDeviceGroup()
        {
            // Arrange
            string deviceId = "ESP32_123";
            string expectedGroup = $"device_{deviceId}";

            // Act
            await _hub.StopMeasurement(deviceId);

            // Assert
            _mockClients.Verify(c => c.Group(expectedGroup), Times.Once);
            _mockClientProxy.Verify(
                p => p.SendCoreAsync("StopTelemetry", It.Is<object?[]>(args => args.Length == 0), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Theory]
        [InlineData(null)]
        [InlineData("")]
        public async Task StopMeasurement_WithNullOrEmptyId_ShouldDoNothing(string? deviceId)
        {
            // Act
            await _hub.StopMeasurement(deviceId!);

            // Assert
            _mockClients.Verify(c => c.Group(It.IsAny<string>()), Times.Never);
            _mockClientProxy.Verify(
                p => p.SendCoreAsync(It.IsAny<string>(), It.IsAny<object?[]>(), It.IsAny<CancellationToken>()),
                Times.Never);
        }

        [Fact]
        public async Task SendTelemetry_WithNullReading_ShouldDoNothing()
        {
            // Act
            await _hub.SendTelemetry(null!);

            // Assert
            _mockProcessor.Verify(p => p.Validate(It.IsAny<LecturaCorazon>()), Times.Never);
            _mockClients.Verify(c => c.Group(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SendTelemetry_WithInvalidReading_ShouldNotBroadcastAndNotSaveToBuffer()
        {
            // Arrange
            var reading = new LecturaCorazon { DispositivoId = "ESP32_123", BPM = 25m, IR = 20000 };
            _mockProcessor.Setup(p => p.Validate(reading)).Returns(false);

            // Act
            await _hub.SendTelemetry(reading);

            // Assert
            _mockProcessor.Verify(p => p.Validate(reading), Times.Once);
            _mockProcessor.Verify(p => p.Smooth(It.IsAny<LecturaCorazon>()), Times.Never);
            _mockProcessor.Verify(p => p.AddToBuffer(It.IsAny<LecturaCorazon>()), Times.Never);
            _mockClients.Verify(c => c.Group(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task SendTelemetry_WithValidReading_ShouldSmoothBufferAndBroadcastToMobileGroup()
        {
            // Arrange
            var reading = new LecturaCorazon { DispositivoId = "ESP32_123", BPM = 75m, IR = 100000 };
            var smoothedReading = new LecturaCorazon { DispositivoId = "ESP32_123", BPM = 75m, IR = 100000, BPMPromedio = 75 };
            string expectedMobileGroup = $"mobile_{reading.DispositivoId}";

            _mockProcessor.Setup(p => p.Validate(reading)).Returns(true);
            _mockProcessor.Setup(p => p.Smooth(reading)).Returns(smoothedReading);

            // Act
            await _hub.SendTelemetry(reading);

            // Assert
            _mockProcessor.Verify(p => p.Validate(reading), Times.Once);
            _mockProcessor.Verify(p => p.Smooth(reading), Times.Once);
            _mockProcessor.Verify(p => p.AddToBuffer(smoothedReading), Times.Once);
            _mockClients.Verify(c => c.Group(expectedMobileGroup), Times.Once);
            _mockClientProxy.Verify(
                p => p.SendCoreAsync("ReceiveTelemetry", It.Is<object?[]>(args => args.Length == 1 && args[0] == smoothedReading), It.IsAny<CancellationToken>()),
                Times.Once);
        }

        [Fact]
        public async Task SendTelemetry_WithReadingMissingDeviceId_ShouldDoNothing()
        {
            // Arrange
            var reading = new LecturaCorazon { DispositivoId = null!, BPM = 75m, IR = 100000 };

            // Act
            await _hub.SendTelemetry(reading);

            // Assert
            _mockProcessor.Verify(p => p.Validate(It.IsAny<LecturaCorazon>()), Times.Never);
            _mockClients.Verify(c => c.Group(It.IsAny<string>()), Times.Never);
        }
    }
}
