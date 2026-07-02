using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.Domain;
using CORSYNC.Core.Interfaces;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Infrastructure.Gamification
{
    public class GamificationService : IGamificationService
    {
        private readonly AdminDbContext _context;

        public GamificationService(AdminDbContext context)
        {
            _context = context;
        }

        public async Task ActualizarProgresoDesafioAsync(int usuarioId, string tipoDesafio, int valorIncremento)
        {
            var challenges = await _context.Desafios
                .Where(d => d.Tipo == tipoDesafio && d.Activo)
                .ToListAsync();

            if (!challenges.Any()) return;

            foreach (var challenge in challenges)
            {
                var progress = await _context.ProgresosDesafios
                    .FirstOrDefaultAsync(pd => pd.UsuarioId == usuarioId && pd.DesafioId == challenge.Id);

                if (progress == null)
                {
                    progress = new ProgresoDesafio
                    {
                        UsuarioId = usuarioId,
                        DesafioId = challenge.Id,
                        ProgresoActual = 0,
                        Completado = false,
                        FechaInicio = DateTime.UtcNow
                    };
                    _context.ProgresosDesafios.Add(progress);
                }

                if (progress.Completado) continue;

                // Calcular progreso según tipo
                if (tipoDesafio == "Racha")
                {
                    progress.ProgresoActual = await CalcularRachaDiasAsync(usuarioId);
                }
                else if (tipoDesafio == "Exploracion")
                {
                    progress.ProgresoActual = await _context.LecturasAura
                        .Where(la => la.UsuarioId == usuarioId)
                        .Select(la => la.AuraDominante)
                        .Distinct()
                        .CountAsync();
                }
                else
                {
                    progress.ProgresoActual += valorIncremento;
                }

                // Verificar si se completó
                if (progress.ProgresoActual >= challenge.MetaObjetivo)
                {
                    progress.ProgresoActual = challenge.MetaObjetivo; // Caper at goal limit
                    progress.Completado = true;
                    progress.FechaCompletado = DateTime.UtcNow;
                }
            }

            await _context.SaveChangesAsync();
        }

        public async Task VerificarMedallasAsync(int usuarioId)
        {
            var unlockedMedalIds = await _context.MedallasUsuarios
                .Where(mu => mu.UsuarioId == usuarioId)
                .Select(mu => mu.MedallaId)
                .ToListAsync();

            var pendingMedals = await _context.Medallas
                .Where(m => !unlockedMedalIds.Contains(m.Id))
                .ToListAsync();

            if (!pendingMedals.Any()) return;

            int totalSessions = await _context.LecturasAura
                .CountAsync(la => la.UsuarioId == usuarioId);

            int racha = await CalcularRachaDiasAsync(usuarioId);

            int completedChallenges = await _context.ProgresosDesafios
                .CountAsync(pd => pd.UsuarioId == usuarioId && pd.Completado);

            bool unlockedAny = false;

            foreach (var medal in pendingMedals)
            {
                bool shouldUnlock = false;

                switch (medal.Condicion)
                {
                    case "PrimeraSesion":
                        shouldUnlock = totalSessions >= medal.ValorCondicion;
                        break;
                    case "SesionesTotales":
                        shouldUnlock = totalSessions >= medal.ValorCondicion;
                        break;
                    case "RachaDias":
                        shouldUnlock = racha >= medal.ValorCondicion;
                        break;
                    case "DesafiosCompletados":
                        shouldUnlock = completedChallenges >= medal.ValorCondicion;
                        break;
                }

                if (shouldUnlock)
                {
                    var unlockedMedal = new MedallaUsuario
                    {
                        UsuarioId = usuarioId,
                        MedallaId = medal.Id,
                        FechaObtenida = DateTime.UtcNow
                    };
                    _context.MedallasUsuarios.Add(unlockedMedal);
                    unlockedAny = true;
                }
            }

            if (unlockedAny)
            {
                await _context.SaveChangesAsync();
            }
        }

        private async Task<int> CalcularRachaDiasAsync(int usuarioId)
        {
            var sessionDates = await _context.LecturasAura
                .Where(la => la.UsuarioId == usuarioId)
                .Select(la => la.FechaFin.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToListAsync();

            if (!sessionDates.Any()) return 0;

            var today = DateTime.UtcNow.Date;
            var lastDate = sessionDates[0];

            if (lastDate == today || lastDate == today.AddDays(-1))
            {
                int racha = 1;
                var currentDate = lastDate;
                for (int i = 1; i < sessionDates.Count; i++)
                {
                    if (sessionDates[i] == currentDate.AddDays(-1))
                    {
                        racha++;
                        currentDate = sessionDates[i];
                    }
                    else
                    {
                        break;
                    }
                }
                return racha;
            }

            return 0;
        }
    }
}
