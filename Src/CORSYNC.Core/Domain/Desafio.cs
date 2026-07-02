using System;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.Domain
{
    public class Desafio
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Titulo { get; set; } = string.Empty;

        [Required]
        [MaxLength(500)]
        public string Descripcion { get; set; } = string.Empty;

        [Required]
        [MaxLength(100)]
        public string Icono { get; set; } = "🎯";

        [Required]
        [MaxLength(50)]
        public string Tipo { get; set; } = string.Empty; // "Sesiones", "Racha", "BpmBajo", "AuraVerde", "Exploracion"

        public int MetaObjetivo { get; set; }

        [Required]
        [MaxLength(50)]
        public string UnidadMedida { get; set; } = string.Empty; // "sesiones", "días", etc.

        public int Puntos { get; set; }

        public bool Activo { get; set; } = true;
    }
}
