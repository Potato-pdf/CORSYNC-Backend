using System;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.DTOs
{
    public class UpdateProfileRequest
    {
        [MaxLength(100, ErrorMessage = "El nombre completo no puede exceder los 100 caracteres.")]
        public string? NombreCompleto { get; set; }

        [MaxLength(100, ErrorMessage = "El nombre espiritual no puede exceder los 100 caracteres.")]
        public string? NombreEspiritual { get; set; }

        [MaxLength(30, ErrorMessage = "El signo zodiacal no puede exceder los 30 caracteres.")]
        public string? SignoZodiacal { get; set; }

        [MaxLength(500, ErrorMessage = "La URL de la foto no puede exceder los 500 caracteres.")]
        public string? FotoUrl { get; set; }
    }

    public class UserStatsResponse
    {
        public decimal BpmPromedio { get; set; }
        public decimal NivelEstresPromedio { get; set; }
        public int SesionesTotales { get; set; }
        public string AuraDominante { get; set; } = string.Empty;
        public int RachaActualDias { get; set; }
        public DateTime? UltimaSesion { get; set; }
    }
}
