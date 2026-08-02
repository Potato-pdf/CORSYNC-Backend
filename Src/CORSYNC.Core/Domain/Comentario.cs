using System;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.Domain
{
    /// <summary>
    /// Valoracion enviada por un cliente sobre la pulsera CORSYNC. Requiere
    /// aprobacion de un administrador antes de publicarse en el sitio.
    /// </summary>
    public class Comentario
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string NombreUsuario { get; set; } = string.Empty;

        [MaxLength(120)]
        public string Email { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        public string Contenido { get; set; } = string.Empty;

        /// <summary>Calificacion de 1 a 5 estrellas.</summary>
        [Range(1, 5)]
        public int Calificacion { get; set; } = 5;

        public int? ProductoId { get; set; }
        public Producto? Producto { get; set; }

        /// <summary>Cliente autenticado que dejo la opinion, si aplica.</summary>
        public int? UsuarioId { get; set; }

        /// <summary>Compra que origina la opinion, si el cliente la dejo desde su panel.</summary>
        public int? CompraClienteId { get; set; }

        public bool Aprobado { get; set; }

        /// <summary>Respuesta publica de ThinkUp a la valoracion.</summary>
        [MaxLength(2000)]
        public string? Respuesta { get; set; }

        public DateTime? FechaRespuesta { get; set; }

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}
