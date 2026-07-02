using System;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.Domain
{
    public class RefreshToken
    {
        public int Id { get; set; }

        [Required]
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        [Required]
        [MaxLength(256)]
        public string Token { get; set; } = string.Empty;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
        
        public DateTime FechaExpiracion { get; set; }
        
        public bool Revocado { get; set; } = false;

        [MaxLength(256)]
        public string? ReemplazadoPor { get; set; }

        public bool EstaActivo => !Revocado && FechaExpiracion > DateTime.UtcNow;
    }
}
