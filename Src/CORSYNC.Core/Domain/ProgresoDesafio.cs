using System;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.Domain
{
    public class ProgresoDesafio
    {
        public int Id { get; set; }

        [Required]
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        [Required]
        public int DesafioId { get; set; }
        public Desafio Desafio { get; set; } = null!;

        public int ProgresoActual { get; set; } = 0;
        
        public bool Completado { get; set; } = false;

        public DateTime FechaInicio { get; set; } = DateTime.UtcNow;
        
        public DateTime? FechaCompletado { get; set; }
    }
}
