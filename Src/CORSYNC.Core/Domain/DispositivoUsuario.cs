using System;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.Domain
{
    public class DispositivoUsuario
    {
        public int Id { get; set; }

        [Required]
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        [Required]
        [MaxLength(50)]
        public string DispositivoId { get; set; } = string.Empty;

        [MaxLength(100)]
        public string NombreDispositivo { get; set; } = "Mi CORSYNC";

        public DateTime FechaVinculacion { get; set; } = DateTime.UtcNow;
        
        public bool Activo { get; set; } = true;
    }
}
