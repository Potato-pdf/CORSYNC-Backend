using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.Domain
{
    /// <summary>Pregunta frecuente publicada en el sitio comercial.</summary>
    public class PreguntaFrecuente
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(300)]
        public string Pregunta { get; set; } = string.Empty;

        [Required]
        [MaxLength(2000)]
        public string Respuesta { get; set; } = string.Empty;

        /// <summary>"Producto", "App movil", "Soporte" o "Ventas".</summary>
        [Required]
        [MaxLength(50)]
        public string Categoria { get; set; } = "Producto";

        public int Orden { get; set; }

        public bool Activo { get; set; } = true;
    }
}
