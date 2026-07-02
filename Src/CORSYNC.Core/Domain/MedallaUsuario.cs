using System;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.Domain
{
    public class MedallaUsuario
    {
        public int Id { get; set; }

        [Required]
        public int UsuarioId { get; set; }
        public Usuario Usuario { get; set; } = null!;

        [Required]
        public int MedallaId { get; set; }
        public Medalla Medalla { get; set; } = null!;

        public DateTime FechaObtenida { get; set; } = DateTime.UtcNow;
    }
}
