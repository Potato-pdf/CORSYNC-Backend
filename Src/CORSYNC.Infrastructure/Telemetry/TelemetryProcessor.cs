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

                // El backend calcula el color del aura dinámicamente
                lectura.Aura = CalculateAura(lectura.BPM, lectura.GsrVoltaje);
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
                double avgGsrRaw = _buffer.Average(l => l.GsrRaw);
                double avgGsrVoltaje = _buffer.Average(l => (double)l.GsrVoltaje);

                decimal consolidatedBpm = Math.Round(avgBpm, 1, MidpointRounding.AwayFromZero);
                decimal consolidatedGsrVoltaje = Math.Round((decimal)avgGsrVoltaje, 3, MidpointRounding.AwayFromZero);

                var consolidated = new LecturaCorazon
                {
                    DispositivoId = deviceId,
                    BPM = consolidatedBpm,
                    IR = (long)Math.Round(avgIr, MidpointRounding.AwayFromZero),
                    BPMPromedio = avgBpmPromedio,
                    GsrRaw = (int)Math.Round(avgGsrRaw, MidpointRounding.AwayFromZero),
                    GsrVoltaje = consolidatedGsrVoltaje,
                    Aura = CalculateAura(consolidatedBpm, consolidatedGsrVoltaje),
                    FechaHora = DateTime.UtcNow
                };

                _buffer.Clear();
                return consolidated;
            }
        }

        private string CalculateAura(decimal bpm, decimal gsrVoltaje)
        {
            // Alta activación física y emocional (Estrés/Enfado/Intensidad)
            if (bpm > 100m && gsrVoltaje > 2.0m)
            {
                return "Roja";
            }
            // Activación física o emocional moderada-alta (Ansiedad/Entusiasmo/Esfuerzo)
            else if (bpm > 85m && gsrVoltaje > 1.5m)
            {
                return "Naranja";
            }
            // Enfoque, concentración o nerviosismo leve (Alerta/Concentración)
            else if (bpm > 75m && gsrVoltaje > 1.0m)
            {
                return "Amarilla";
            }
            // Estado neutro, tranquilidad normal (Calma/Estabilidad)
            else if (bpm >= 65m && gsrVoltaje >= 0.5m)
            {
                return "Verde";
            }
            // Estado de relajación profunda (Paz/Meditación)
            else if (bpm < 65m || gsrVoltaje < 0.5m)
            {
                return "Azul";
            }

            return "Verde"; // Default
        }
    }
}
