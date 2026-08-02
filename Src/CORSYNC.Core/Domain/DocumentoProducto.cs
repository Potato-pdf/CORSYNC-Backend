using System;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.Domain
{
    /// <summary>
    /// Documentacion asociada al producto (manuales, guias rapidas, fichas tecnicas)
    /// disponible para los clientes que lo adquirieron.
    /// </summary>
    public class DocumentoProducto
    {
        public int Id { get; set; }

        [Required]
        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }

        [Required]
        [MaxLength(150)]
        public string Titulo { get; set; } = string.Empty;

        [MaxLength(400)]
        public string Descripcion { get; set; } = string.Empty;

        /// <summary>"Manual", "Guia", "FichaTecnica", "Garantia" o "Video".</summary>
        [Required]
        [MaxLength(30)]
        public string Tipo { get; set; } = "Manual";

        [MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        [MaxLength(20)]
        public string? Peso { get; set; }

        public DateTime FechaPublicacion { get; set; } = DateTime.UtcNow;
    }
}
