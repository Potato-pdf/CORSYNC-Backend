using System;

namespace CORSYNC.Core.Domain
{
    public class Comentario
    {
        public int Id { get; set; }
        public string NombreUsuario { get; set; } = string.Empty;
        public string Contenido { get; set; } = string.Empty;
        public bool Aprobado { get; set; }
        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}
