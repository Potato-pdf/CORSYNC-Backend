using Xunit;
using CORSYNC.Core.Domain;
using CORSYNC.Infrastructure.Telemetry;

namespace CORSYNC.Tests
{
    public class TelemetryProcessorTests
    {
        private readonly TelemetryProcessor _processor;

        public TelemetryProcessorTests()
        {
            _processor = new TelemetryProcessor();
        }

        [Fact]
        public void Validate_WithValidReading_ShouldReturnTrue()
        {
            // Arrange
            var reading = new LecturaCorazon { BPM = 72m, IR = 100000 };

            // Act
            bool result = _processor.Validate(reading);

            // Assert
            Assert.True(result);
        }

        [Theory]
        [InlineData(25.0)]   // Under human bounds
        [InlineData(230.0)]  // Over human bounds
        public void Validate_WithOutlierBpm_ShouldReturnFalse(double bpm)
        {
            // Arrange
            var reading = new LecturaCorazon { BPM = (decimal)bpm, IR = 100000 };

            // Act
            bool result = _processor.Validate(reading);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Validate_WithContactLost_ShouldReturnFalse()
        {
            // Arrange
            var reading = new LecturaCorazon { BPM = 80m, IR = 25000 }; // IR below 50000

            // Act
            bool result = _processor.Validate(reading);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void Smooth_ShouldComputeMovingAverageOfLastFiveValues()
        {
            // Arrange
            var readings = new[]
            {
                new LecturaCorazon { BPM = 60m },
                new LecturaCorazon { BPM = 70m },
                new LecturaCorazon { BPM = 80m },
                new LecturaCorazon { BPM = 90m },
                new LecturaCorazon { BPM = 100m },
                new LecturaCorazon { BPM = 110m } // First one (60) should drop out
            };

            // Act & Assert
            // 1. First reading: average = 60
            var r1 = _processor.Smooth(readings[0]);
            Assert.Equal(60, r1.BPMPromedio);

            // 2. Average of 60, 70 = 65
            var r2 = _processor.Smooth(readings[1]);
            Assert.Equal(65, r2.BPMPromedio);

            // 3. Average of 60, 70, 80 = 70
            var r3 = _processor.Smooth(readings[2]);
            Assert.Equal(70, r3.BPMPromedio);

            // 4. Average of 60, 70, 80, 90 = 75
            var r4 = _processor.Smooth(readings[3]);
            Assert.Equal(75, r4.BPMPromedio);

            // 5. Average of 60, 70, 80, 90, 100 = 80
            var r5 = _processor.Smooth(readings[4]);
            Assert.Equal(80, r5.BPMPromedio);

            // 6. Sliding window drops 60. Window is now: 70, 80, 90, 100, 110. Average = 90
            var r6 = _processor.Smooth(readings[5]);
            Assert.Equal(90, r6.BPMPromedio);
        }

        [Fact]
        public void FlushBuffer_WithEmptyBuffer_ShouldReturnNull()
        {
            // Act
            var result = _processor.FlushBuffer();

            // Assert
            Assert.Null(result);
        }

        [Fact]
        public void FlushBuffer_WithMultipleReadings_ShouldReturnAveragedConsolidatedReading()
        {
            // Arrange
            var r1 = new LecturaCorazon { DispositivoId = "ESP32_Test", BPM = 70m, IR = 100000, BPMPromedio = 70 };
            var r2 = new LecturaCorazon { DispositivoId = "ESP32_Test", BPM = 80m, IR = 110000, BPMPromedio = 75 };
            
            _processor.AddToBuffer(r1);
            _processor.AddToBuffer(r2);

            // Act
            var consolidated = _processor.FlushBuffer();

            // Assert
            Assert.NotNull(consolidated);
            Assert.Equal("ESP32_Test", consolidated!.DispositivoId);
            Assert.Equal(75m, consolidated.BPM); // (70+80)/2
            Assert.Equal(105000L, consolidated.IR); // (100000+110000)/2
            Assert.Equal(73, consolidated.BPMPromedio); // Round(72.5) = 73
            
            // Further flushes should be null
            Assert.Null(_processor.FlushBuffer());
        }
    }
}
