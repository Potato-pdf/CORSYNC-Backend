using System;
using System.Collections.Generic;

namespace CORSYNC.Core.DTOs
{
    public class TrendDataPointResponse
    {
        public string Fecha { get; set; } = string.Empty;
        public decimal BpmPromedio { get; set; }
        public decimal BpmMaximo { get; set; }
        public decimal BpmMinimo { get; set; }
        public decimal EstresPromedio { get; set; }
        public decimal GsrPromedio { get; set; }
        public int Sesiones { get; set; }
        public int DuracionPromedioSeg { get; set; }
    }

    public class TrendsResponse
    {
        public string Period { get; set; } = string.Empty;
        public List<TrendDataPointResponse> DataPoints { get; set; } = new();
    }

    public class DistributionResponse
    {
        public Dictionary<string, int> DistribucionAuras { get; set; } = new();
        public Dictionary<string, int> DistribucionEstres { get; set; } = new();
        public Dictionary<string, int> DistribucionBpm { get; set; } = new();
    }

    public class WeekSummaryResponse
    {
        public decimal BpmPromedio { get; set; }
        public decimal EstresPromedio { get; set; }
        public int Sesiones { get; set; }
    }

    public class ComparisonResponse
    {
        public WeekSummaryResponse SemanaActual { get; set; } = new();
        public WeekSummaryResponse SemanaAnterior { get; set; } = new();
        public decimal BpmCambioPct { get; set; }
        public decimal EstresCambioPct { get; set; }
        public decimal SesionesCambioPct { get; set; }
        public string Tendencia { get; set; } = string.Empty;
    }
}
