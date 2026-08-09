using System;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.Domain
{
    /// <summary>
    /// Bitacora de correos generados por el sistema. Permite al administrador
    /// consultar y reenviar las credenciales entregadas a los clientes.
    /// </summary>
    public class CorreoEnviado
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Destinatario { get; set; } = string.Empty;

        [Required]
        [MaxLength(200)]
        public string Asunto { get; set; } = string.Empty;

        [Required]
        [MaxLength(4000)]
        public string Cuerpo { get; set; } = string.Empty;

        /// <summary>"Credenciales", "RestablecerPassword" o "Notificacion".</summary>
        [Required]
        [MaxLength(40)]
        public string Tipo { get; set; } = "Notificacion";

        /// <summary>"Simulado" cuando no hay SMTP configurado, "Enviado" o "Error".</summary>
        [Required]
        [MaxLength(20)]
        public string Estado { get; set; } = "Simulado";

        public DateTime FechaEnvio { get; set; } = DateTime.UtcNow;
    }
}
