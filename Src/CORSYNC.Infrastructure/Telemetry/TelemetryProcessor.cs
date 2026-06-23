using System;
using System.Collections.Generic;
using System.Linq;
using CORSYNC.Core.Domain;
using CORSYNC.Core.Interfaces;

namespace CORSYNC.Infrastructure.Telemetry
{
    public class TelemetryProcessor : ITelemetryProcessor
    {
        private readonly Queue<decimal> _bpmWindow = new();
        private const int WindowSize = 5;
        private readonly List<LecturaCorazon> _buffer = new();
        private readonly object _lock = new();

        public bool Validate(LecturaCorazon lectura)
        {
            if (lectura == null) return false;
            
            // Filter outliers (pulse rate outside human bounds)
            if (lectura.BPM < 30m || lectura.BPM > 220m)
            {
                return false;
            }

            // Validate contact (IR signal strength minimum base)
            if (lectura.IR < 50000)
            {
                return false;
            }

            return true;
        }

        public LecturaCorazon Smooth(LecturaCorazon lectura)
        {
            if (lectura == null) throw new ArgumentNullException(nameof(lectura));

            lock (_lock)
            {
                _bpmWindow.Enqueue(lectura.BPM);
                if (_bpmWindow.Count > WindowSize)
                {
                    _bpmWindow.Dequeue();
                }

                decimal avgBpm = _bpmWindow.Average();
                lectura.BPMPromedio = (int)Math.Round(avgBpm, MidpointRounding.AwayFromZero);
            }

            return lectura;
        }

        public void AddToBuffer(LecturaCorazon lectura)
        {
            if (lectura == null) return;
            lock (_lock)
            {
                _buffer.Add(lectura);
            }
        }

        public LecturaCorazon? FlushBuffer()
        {
            lock (_lock)
            {
                if (_buffer.Count == 0)
                {
                    return null;
                }

                string deviceId = _buffer.First().DispositivoId;
                decimal avgBpm = _buffer.Average(l => l.BPM);
                double avgIr = _buffer.Average(l => l.IR);
                int avgBpmPromedio = (int)Math.Round(_buffer.Average(l => l.BPMPromedio), MidpointRounding.AwayFromZero);

                var consolidated = new LecturaCorazon
                {
                    DispositivoId = deviceId,
                    BPM = Math.Round(avgBpm, 1, MidpointRounding.AwayFromZero),
                    IR = (long)Math.Round(avgIr, MidpointRounding.AwayFromZero),
                    BPMPromedio = avgBpmPromedio,
                    FechaHora = DateTime.UtcNow
                };

                _buffer.Clear();
                return consolidated;
            }
        }
    }
}
