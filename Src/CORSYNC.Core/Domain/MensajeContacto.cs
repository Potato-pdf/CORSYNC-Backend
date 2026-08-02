using System;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.Domain
{
    /// <summary>Mensaje recibido desde el formulario publico de contacto.</summary>
    public class MensajeContacto
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(40)]
        public string? Telefono { get; set; }

        [Required]
        [MaxLength(150)]
        public string Asunto { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        public string Mensaje { get; set; } = string.Empty;

        public bool Atendido { get; set; }

        public DateTime FechaEnvio { get; set; } = DateTime.UtcNow;
    }
}
