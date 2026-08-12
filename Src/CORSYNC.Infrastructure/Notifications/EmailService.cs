using System;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using CORSYNC.Core.Domain;
using CORSYNC.Core.Interfaces;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Infrastructure.Notifications
{
    /// <inheritdoc cref="IEmailService"/>
    public class EmailService : IEmailService
    {
        private const string CaracteresPassword = "abcdefghijkmnpqrstuvwxyzABCDEFGHJKLMNPQRSTUVWXYZ23456789";

        private readonly AdminDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly ILogger<EmailService> _logger;

        public EmailService(AdminDbContext context, IConfiguration configuration, ILogger<EmailService> logger)
        {
            _context = context;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<ResultadoCorreo> EnviarAsync(string destinatario, string asunto, string cuerpo, string tipo)
        {
            var resultado = new ResultadoCorreo();

            var host = _configuration["Smtp:Host"];
            var habilitado = LeerBool("Smtp:Habilitado", false);

            if (habilitado && !string.IsNullOrWhiteSpace(host))
            {
                try
                {
                    var puerto = int.TryParse(_configuration["Smtp:Port"], out int p) ? p : 587;
                    var usuario = _configuration["Smtp:User"] ?? string.Empty;
                    var password = _configuration["Smtp:Password"] ?? string.Empty;
                    var remitente = _configuration["Smtp:From"] ?? usuario;

                    using var cliente = new SmtpClient(host, puerto)
                    {
                        EnableSsl = LeerBool("Smtp:EnableSsl", true),
                        Credentials = new NetworkCredential(usuario, password)
                    };

                    using var mensaje = new MailMessage(remitente, destinatario, asunto, cuerpo) { IsBodyHtml = false };
                    await cliente.SendMailAsync(mensaje);

                    resultado.Enviado = true;
                    resultado.Estado = "Enviado";
                    resultado.Mensaje = $"Correo enviado a {destinatario}.";
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "No se pudo enviar el correo a {Destinatario}", destinatario);
                    resultado.Enviado = false;
                    resultado.Estado = "Error";
                    resultado.Mensaje = $"No se pudo enviar el correo: {ex.Message}";
                }
            }
            else
            {
                // Sin SMTP configurado el correo queda registrado en la bitacora para que
                // el administrador entregue las credenciales manualmente. Se distingue
                // que falta exactamente: "no configurado" a secas obligaba a adivinar si
                // el problema era el interruptor, el host o unos secretos sin cargar.
                var motivo = !habilitado
                    ? "Smtp:Habilitado esta en false"
                    : "Smtp:Host esta vacio";

                resultado.Enviado = false;
                resultado.Estado = "Simulado";
                resultado.Mensaje =
                    $"SMTP no configurado ({motivo}). El correo para {destinatario} quedo registrado " +
                    "en la bitacora del panel. Revisa la seccion \"Correo\" del README: los user secrets " +
                    "solo se cargan con ASPNETCORE_ENVIRONMENT=Development.";

                _logger.LogWarning(
                    "Correo simulado para {Destinatario} ({Motivo}). Asunto: {Asunto}",
                    destinatario, motivo, asunto);
            }

            _context.CorreosEnviados.Add(new CorreoEnviado
            {
                Destinatario = destinatario,
                Asunto = asunto,
                Cuerpo = cuerpo,
                Tipo = tipo,
                Estado = resultado.Estado,
                FechaEnvio = DateTime.UtcNow
            });
            await _context.SaveChangesAsync();

            return resultado;
        }

        private bool LeerBool(string clave, bool porDefecto) =>
            bool.TryParse(_configuration[clave], out bool valor) ? valor : porDefecto;

        public string GenerarPasswordTemporal()
        {
            // 10 caracteres sin simbolos ambiguos (l, I, O, 0, 1) para dictarlos sin error.
            Span<char> buffer = stackalloc char[10];
            for (int i = 0; i < buffer.Length; i++)
            {
                buffer[i] = CaracteresPassword[RandomNumberGenerator.GetInt32(CaracteresPassword.Length)];
            }
            return new string(buffer);
        }
    }
}
