using System;
using System.Collections.Generic;

namespace CORSYNC.Core.DTOs
{
    public class RecommendationItemResponse
    {
        public int Id { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Titulo { get; set; } = string.Empty;
        public string Descripcion { get; set; } = string.Empty;
        public string Prioridad { get; set; } = string.Empty;
        public string Icono { get; set; } = string.Empty;
        public int DuracionMinutos { get; set; }
        public string Categoria { get; set; } = string.Empty;
    }

    public class SuggestedChallengeResponse
    {
        public int ChallengeId { get; set; }
        public string Titulo { get; set; } = string.Empty;
        public string Razon { get; set; } = string.Empty;
        public double PrioridadMatch { get; set; }
    }

    public class RecommendationsPackageResponse
    {
        public string NivelEstresActual { get; set; } = string.Empty;
        public decimal ScoreEstres { get; set; }
        public Dictionary<string, string> CategoriasBienestar { get; set; } = new();
        public List<RecommendationItemResponse> Recomendaciones { get; set; } = new();
        public List<SuggestedChallengeResponse> DesafiosSugeridos { get; set; } = new();
        public string MensajeMotivacional { get; set; } = string.Empty;
    }
}
