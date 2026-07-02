using System;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.DTOs
{
    public class ChallengeResponse
    {
        public int Id { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Icono { get; set; } = string.Empty;
        public string Tipo { get; set; } = string.Empty;
        public int MetaObjetivo { get; set; }
        public string UnidadMedida { get; set; } = string.Empty;
        public int Puntos { get; set; }
        
        // Progreso del usuario
        public int ProgresoActual { get; set; }
        public bool Completado { get; set; }
        public double PorcentajeProgreso { get; set; }
        public DateTime? FechaCompletado { get; set; }
    }

    public class UpdateProgressRequest
    {
        [Required(ErrorMessage = "El progreso actual es requerido.")]
        [Range(0, int.MaxValue, ErrorMessage = "El progreso actual debe ser mayor o igual a 0.")]
        public int ProgresoActual { get; set; }
    }

    public class MedalResponse
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Icono { get; set; } = string.Empty;
        public DateTime FechaObtenida { get; set; }
    }
}
