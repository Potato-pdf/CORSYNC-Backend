using System;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.Domain
{
    public class Medalla
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Nombre { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Descripcion { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Icono { get; set; } = "🏅";

        [Required]
        [MaxLength(50)]
        public string Condicion { get; set; } = string.Empty; // "SesionesTotales", "RachaDias", "DesafiosCompletados", "PrimeraSesion"

        public int ValorCondicion { get; set; }
    }
}
