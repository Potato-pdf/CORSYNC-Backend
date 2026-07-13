using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.DTOs;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Api.Controllers
{
    [Authorize]
    [ApiController]
    [Route("api/[controller]")]
    public class RecommendationsController : ControllerBase
    {
        private readonly AdminDbContext _context;

        public RecommendationsController(AdminDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetRecommendations()
        {
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized("Identificador de usuario inválido en el token.");
            }

            // Consultar las últimas 5 lecturas para calcular un promedio reciente de estrés
            var recentReadings = await _context.LecturasAura
                .Where(la => la.UsuarioId == userId)
                .OrderByDescending(la => la.FechaFin)
                .Take(5)
                .ToListAsync();

            decimal scoreEstres = recentReadings.Any() ? recentReadings.Average(la => la.NivelEstres) : 0;
            decimal scoreBpm = recentReadings.Any() ? recentReadings.Average(la => la.BpmPromedio) : 0;

            var recommendations = new List<RecommendationItemResponse>();
            var suggestedChallenges = new List<SuggestedChallengeResponse>();
            string mensajeMotivacional = "";
            string nivelEstresActual = "";

            if (!recentReadings.Any())
            {
                nivelEstresActual = "Bajo";
                mensajeMotivacional = "¡Te damos la bienvenida a CORSYNC! Realiza tu primer escaneo para conocer tu nivel de estrés.";

                recommendations.Add(new RecommendationItemResponse
                {
                    Id = 1,
                    Tipo = "Respiracion",
                    Titulo = "Respiración Diafragmática",
                    Descripcion = "Coloca una mano en tu pecho y otra en tu abdomen. Respira lentamente sintiendo cómo se expande el abdomen.",
                    Prioridad = "Baja",
                    Icono = "🌬️",
                    DuracionMinutos = 5,
                    Categoria = "Iniciación"
                });
                recommendations.Add(new RecommendationItemResponse
                {
                    Id = 2,
                    Tipo = "Actividad",
                    Titulo = "Primer Escaneo",
                    Descripcion = "Realiza un escaneo de 20 segundos para sincronizar tus pulsaciones con el espejo.",
                    Prioridad = "Alta",
                    Icono = "🔮",
                    DuracionMinutos = 1,
                    Categoria = "Sincronización"
                });

                suggestedChallenges.Add(new SuggestedChallengeResponse
                {
                    ChallengeId = 1,
                    Titulo = "Primera Lectura",
                    Razon = "Es el primer paso para comenzar tu viaje de bienestar.",
                    PrioridadMatch = 1.0
                });
            }
            else
            {
                if (scoreEstres < 20)
                {
                    nivelEstresActual = "Muy Bajo";
                    mensajeMotivacional = "¡Increíble! Tu energía está en perfecta calma y equilibrio. Sigue cultivando estos momentos.";

                    recommendations.Add(new RecommendationItemResponse
                    {
                        Id = 10,
                        Tipo = "Celebracion",
                        Titulo = "Celebrar el Equilibrio",
                        Descripcion = "Disfruta de este estado de paz. Si deseas, compártelo con tus seres queridos o continúa con tu racha de meditación.",
                        Prioridad = "Baja",
                        Icono = "🟢",
                        DuracionMinutos = 0,
                        Categoria = "Mantener"
                    });
                    recommendations.Add(new RecommendationItemResponse
                    {
                        Id = 11,
                        Tipo = "Constancia",
                        Titulo = "Mantén tu Racha",
                        Descripcion = "La constancia es la clave del bienestar a largo plazo. Agenda tu próxima sesión para mañana a la misma hora.",
                        Prioridad = "Media",
                        Icono = "📅",
                        DuracionMinutos = 2,
                        Categoria = "Consistencia"
                    });

                    suggestedChallenges.Add(new SuggestedChallengeResponse
                    {
                        ChallengeId = 4,
                        Titulo = "Semana Zen",
                        Razon = "Tu nivel de estrés es excelente. ¡Mantén este ritmo y completa tu racha de 7 días!",
                        PrioridadMatch = 0.95
                    });
                }
                else if (scoreEstres >= 20 && scoreEstres < 40)
                {
                    nivelEstresActual = "Bajo";
                    mensajeMotivacional = "Tu estrés es bajo. Es un excelente momento para reforzar tus hábitos saludables.";

                    recommendations.Add(new RecommendationItemResponse
                    {
                        Id = 20,
                        Tipo = "Actividad",
                        Titulo = "Caminata de Consciencia",
                        Descripcion = "Camina al aire libre durante 10 minutos sin distracciones digitales, observando tu entorno.",
                        Prioridad = "Baja",
                        Icono = "🚶",
                        DuracionMinutos = 10,
                        Categoria = "Movimiento"
                    });
                    recommendations.Add(new RecommendationItemResponse
                    {
                        Id = 21,
                        Tipo = "Reflexion",
                        Titulo = "Journaling (Diario)",
                        Descripcion = "Escribe tres cosas por las que estás agradecido el día de hoy en tu diario energético.",
                        Prioridad = "Media",
                        Icono = "📓",
                        DuracionMinutos = 5,
                        Categoria = "Mente"
                    });

                    suggestedChallenges.Add(new SuggestedChallengeResponse
                    {
                        ChallengeId = 6,
                        Titulo = "Corazón Sereno",
                        Razon = "Tienes un pulso y estrés estables. Completa 5 sesiones tranquilas para este desafío.",
                        PrioridadMatch = 0.85
                    });
                }
                else if (scoreEstres >= 40 && scoreEstres < 60)
                {
                    nivelEstresActual = "Moderado";
                    mensajeMotivacional = "Tu nivel de estrés es moderado. Tómate un breve respiro para recargar energías y evitar que aumente.";

                    recommendations.Add(new RecommendationItemResponse
                    {
                        Id = 30,
                        Tipo = "Respiracion",
                        Titulo = "Respiración Cuadrada (Sama Vritti)",
                        Descripcion = "Inhala durante 4 segundos, retén 4 segundos, exhala 4 segundos y mantén vacío 4 segundos. Repite 5 veces.",
                        Prioridad = "Media",
                        Icono = "🌬️",
                        DuracionMinutos = 3,
                        Categoria = "Prevenir"
                    });
                    recommendations.Add(new RecommendationItemResponse
                    {
                        Id = 31,
                        Tipo = "Relajacion",
                        Titulo = "Estiramientos del Tren Superior",
                        Descripcion = "Realiza círculos suaves con el cuello, hombros y estira la espalda para liberar tensión física.",
                        Prioridad = "Media",
                        Icono = "🧘",
                        DuracionMinutos = 5,
                        Categoria = "Relajación"
                    });

                    suggestedChallenges.Add(new SuggestedChallengeResponse
                    {
                        ChallengeId = 7,
                        Titulo = "Aura Verde Pura",
                        Razon = "Tu estrés es moderado. Conectarte con el aura Verde (Calma) te ayudará a equilibrarte.",
                        PrioridadMatch = 0.88
                    });
                }
                else if (scoreEstres >= 60 && scoreEstres < 80)
                {
                    nivelEstresActual = "Alto";
                    mensajeMotivacional = "Atención: Tu nivel de estrés es alto. Te recomendamos detenerte unos minutos y realizar ejercicios de respiración.";

                    recommendations.Add(new RecommendationItemResponse
                    {
                        Id = 40,
                        Tipo = "Respiracion",
                        Titulo = "Respiración 4-7-8",
                        Descripcion = "Inhala silenciosamente por la nariz durante 4 segundos, mantén el aire 7 segundos y exhala completamente con un soplido durante 8 segundos. Repite 4 ciclos.",
                        Prioridad = "Alta",
                        Icono = "🌬️",
                        DuracionMinutos = 4,
                        Categoria = "Reducción de Estrés"
                    });
                    recommendations.Add(new RecommendationItemResponse
                    {
                        Id = 41,
                        Tipo = "Meditacion",
                        Titulo = "Escaneo Corporal Corto",
                        Descripcion = "Cierra los ojos, respira profundo y enfoca tu atención en relajar la mandíbula, los hombros y el abdomen.",
                        Prioridad = "Alta",
                        Icono = "🧘",
                        DuracionMinutos = 5,
                        Categoria = "Calma Activa"
                    });

                    suggestedChallenges.Add(new SuggestedChallengeResponse
                    {
                        ChallengeId = 7,
                        Titulo = "Aura Verde Pura",
                        Razon = "Bajar el estrés es primordial. Alcanzar el aura Verde te ayudará a calmar tu sistema nervioso.",
                        PrioridadMatch = 0.95
                    });
                }
                else
                {
                    nivelEstresActual = "Muy Alto";
                    mensajeMotivacional = "Alerta de Estrés: Tu nivel de estrés es crítico. Por favor, realiza una pausa activa inmediata y concéntrate en tu respiración.";

                    recommendations.Add(new RecommendationItemResponse
                    {
                        Id = 50,
                        Tipo = "Respiracion",
                        Titulo = "Respiración 4-7-8 (Urgente)",
                        Descripcion = "Inhala 4s, retén 7s y exhala 8s. Concéntrate únicamente en el sonido del aire al salir. Hazlo ahora mismo por al menos 5 ciclos.",
                        Prioridad = "Alta",
                        Icono = "🚨",
                        DuracionMinutos = 5,
                        Categoria = "Intervención"
                    });
                    recommendations.Add(new RecommendationItemResponse
                    {
                        Id = 51,
                        Tipo = "Desconexion",
                        Titulo = "Pausa Digital Absoluta",
                        Descripcion = "Apaga o aleja tu teléfono celular y computadora. Camina o siéntate en silencio bebiendo un vaso de agua lentamente.",
                        Prioridad = "Alta",
                        Icono = "📴",
                        DuracionMinutos = 15,
                        Categoria = "Desconexión"
                    });

                    suggestedChallenges.Add(new SuggestedChallengeResponse
                    {
                        ChallengeId = 6,
                        Titulo = "Corazón Sereno",
                        Razon = "Tu nivel de estrés actual es muy alto. El desafío 'Corazón Sereno' te guiará a buscar sesiones con pulso relajado.",
                        PrioridadMatch = 0.99
                    });
                }
            }

            var categoriasBienestar = new Dictionary<string, string>
            {
                { "fisico", scoreBpm > 100 ? "Elevado" : (scoreBpm < 60 && scoreBpm > 0 ? "Bradicardia" : "Normal") },
                { "mental", nivelEstresActual },
                { "consistencia", recentReadings.Count >= 3 ? "Buena" : "Baja" }
            };

            return Ok(new RecommendationsPackageResponse
            {
                NivelEstresActual = nivelEstresActual,
                ScoreEstres = Math.Round(scoreEstres, 1),
                CategoriasBienestar = categoriasBienestar,
                Recomendaciones = recommendations,
                DesafiosSugeridos = suggestedChallenges,
                MensajeMotivacional = mensajeMotivacional
            });
        }
    }
}
