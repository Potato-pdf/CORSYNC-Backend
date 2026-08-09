using System.Threading.Tasks;

namespace CORSYNC.Core.Interfaces
{
    public class ResultadoCorreo
    {
        public bool Enviado { get; set; }
        /// <summary>"Simulado", "Enviado" o "Error".</summary>
        public string Estado { get; set; } = "Simulado";
        public string Mensaje { get; set; } = string.Empty;
    }

    /// <summary>
    /// Entrega de correo del sistema. Sin credenciales SMTP configuradas el correo se
    /// registra en la bitacora (CorreosEnviados) para que el administrador lo consulte
    /// desde el panel; al configurar SMTP en appsettings.json el envio se vuelve real.
    /// </summary>
    public interface IEmailService
    {
        Task<ResultadoCorreo> EnviarAsync(string destinatario, string asunto, string cuerpo, string tipo);

        /// <summary>Genera una contrasena temporal legible para entregar a un cliente nuevo.</summary>
        string GenerarPasswordTemporal();
    }
}
