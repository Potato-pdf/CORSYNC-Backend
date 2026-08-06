using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.Domain
{
    /// <summary>
    /// Característica destacada del producto, de las que se listan con un icono en
    /// la ficha comercial ("Sensor de respuesta galvánica", "Batería de 7 días"...).
    /// </summary>
    public class CaracteristicaProducto
    {
        public int Id { get; set; }

        [Required]
        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }

        [Required]
        [MaxLength(200)]
        public string Texto { get; set; } = string.Empty;

        /// <summary>Nombre del icono de Bootstrap Icons, sin el prefijo "bi-".</summary>
        [MaxLength(60)]
        public string Icono { get; set; } = "check-lg";

        public int Orden { get; set; }
    }

    /// <summary>
    /// Renglón de la ficha técnica. Se agrupa por <see cref="Grupo"/> para armar las
    /// columnas de especificaciones (Físicas, Sensores, Sistema...).
    /// </summary>
    public class EspecificacionProducto
    {
        public int Id { get; set; }

        [Required]
        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }

        [Required]
        [MaxLength(80)]
        public string Grupo { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Campo { get; set; } = string.Empty;

        [Required]
        [MaxLength(250)]
        public string Valor { get; set; } = string.Empty;

        public int Orden { get; set; }
    }
}
