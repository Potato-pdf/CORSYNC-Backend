using System;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.Domain
{
    public class Proveedor
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(120)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(40)]
        public string Telefono { get; set; } = string.Empty;

        /// <summary>Persona de contacto dentro del proveedor.</summary>
        [MaxLength(120)]
        public string Contacto { get; set; } = string.Empty;

        [MaxLength(250)]
        public string Direccion { get; set; } = string.Empty;

        [MaxLength(80)]
        public string Pais { get; set; } = string.Empty;

        public bool Activo { get; set; } = true;

        public DateTime FechaAlta { get; set; } = DateTime.UtcNow;
    }
}
