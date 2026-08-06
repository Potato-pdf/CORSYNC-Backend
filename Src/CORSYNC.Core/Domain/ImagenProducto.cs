using System;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.Domain
{
    /// <summary>
    /// Imagen de la galería de un producto. El archivo vive en
    /// wwwroot/uploads/productos/{productoId}/ y aquí se guarda su ruta pública.
    /// </summary>
    public class ImagenProducto
    {
        public int Id { get; set; }

        [Required]
        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }

        /// <summary>Ruta pública servida por el backend, p. ej. /uploads/productos/1/abc.jpg</summary>
        [Required]
        [MaxLength(500)]
        public string Url { get; set; } = string.Empty;

        /// <summary>Texto alternativo de la imagen; también sirve de pie en el carrusel.</summary>
        [MaxLength(200)]
        public string Titulo { get; set; } = string.Empty;

        [MaxLength(400)]
        public string Descripcion { get; set; } = string.Empty;

        /// <summary>Posición dentro del carrusel. Menor va primero.</summary>
        public int Orden { get; set; }

        /// <summary>Nombre del archivo en disco, necesario para poder borrarlo.</summary>
        [MaxLength(260)]
        public string NombreArchivo { get; set; } = string.Empty;

        public long TamanoBytes { get; set; }

        public DateTime FechaSubida { get; set; } = DateTime.UtcNow;
    }
}
