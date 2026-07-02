using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.DTOs
{
    public class CreateReadingRequest
    {
        [Required(ErrorMessage = "El identificador de dispositivo es requerido.")]
        [MaxLength(50)]
        public string DispositivoId { get; set; } = string.Empty;

        [Range(30, 220, ErrorMessage = "El BPM promedio debe estar entre 30 y 220.")]
        public decimal BpmPromedio { get; set; }

        [Range(30, 220, ErrorMessage = "El BPM máximo debe estar entre 30 y 220.")]
        public decimal BpmMaximo { get; set; }

        [Range(30, 220, ErrorMessage = "El BPM mínimo debe estar entre 30 y 220.")]
        public decimal BpmMinimo { get; set; }

        public int GsrRawPromedio { get; set; }

        [Range(0.0, 3.3, ErrorMessage = "El voltaje GSR debe estar entre 0.0V y 3.3V.")]
        public decimal GsrVoltajePromedio { get; set; }

        [Range(0.0, 100.0, ErrorMessage = "El nivel de estrés debe estar entre 0.0 y 100.0.")]
        public decimal NivelEstres { get; set; }

        [Required(ErrorMessage = "El aura dominante es requerida.")]
        [MaxLength(50)]
        public string AuraDominante { get; set; } = string.Empty;

        [MaxLength(500)]
        public string? Notas { get; set; }

        public int DuracionSegundos { get; set; }

        public DateTime FechaInicio { get; set; }

        public DateTime FechaFin { get; set; }
    }

    public class ReadingResponse
    {
        public int Id { get; set; }
        public string DispositivoId { get; set; } = string.Empty;
        public decimal BpmPromedio { get; set; }
        public decimal BpmMaximo { get; set; }
        public decimal BpmMinimo { get; set; }
        public int GsrRawPromedio { get; set; }
        public decimal GsrVoltajePromedio { get; set; }
        public decimal NivelEstres { get; set; }
        public string AuraDominante { get; set; } = string.Empty;
        public string? Notas { get; set; }
        public int DuracionSegundos { get; set; }
        public DateTime FechaInicio { get; set; }
        public DateTime FechaFin { get; set; }
    }

    public class ReadingSummaryResponse
    {
        public decimal BpmPromedioGlobal { get; set; }
        public decimal NivelEstresPromedio { get; set; }
        public int TotalSesiones { get; set; }
        public string AuraMasFrecuente { get; set; } = string.Empty;
        public Dictionary<string, int> DistribucionAuras { get; set; } = new();
    }
}
