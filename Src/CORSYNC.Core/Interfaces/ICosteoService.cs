using System.Collections.Generic;
using System.Threading.Tasks;
using CORSYNC.Core.DTOs;

namespace CORSYNC.Core.Interfaces
{
    /// <summary>
    /// Implementa el metodo de costeo de la empresa: costo promedio ponderado para la
    /// materia prima y costeo absorbente para el producto terminado.
    /// </summary>
    public interface ICosteoService
    {
        /// <summary>
        /// Explosiona la receta del producto, valua cada insumo al costo promedio
        /// ponderado vigente y agrega mano de obra y gastos indirectos de fabricacion.
        /// </summary>
        Task<CostoProductoResponse?> CalcularCostoProductoAsync(int productoId);

        /// <summary>
        /// Recalcula el costo promedio ponderado de un insumo al recibir una compra:
        /// (stock * costo actual + cantidad recibida * costo de compra) / stock total.
        /// </summary>
        Task<ImpactoCosteoResponse?> RegistrarEntradaInventarioAsync(int materiaPrimaId, decimal cantidad, decimal costoCompra);
    }

    /// <summary>Reglas comerciales de precio publicadas por el area de ventas de ThinkUp.</summary>
    public static class ReglasComerciales
    {
        public const decimal TasaImpuesto = 0.16m;

        /// <summary>Factor de precio segun el tipo de licencia contratada.</summary>
        public static decimal FactorLicencia(string? tipoLicencia) => (tipoLicencia ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "corporativa" => 0.90m,
            "enterprise" => 0.83m,
            _ => 1.00m
        };

        public static string NormalizarLicencia(string? tipoLicencia) => (tipoLicencia ?? string.Empty).Trim().ToLowerInvariant() switch
        {
            "corporativa" => "Corporativa",
            "enterprise" => "Enterprise",
            _ => "Individual"
        };

        /// <summary>Descuento por volumen aplicado sobre el subtotal.</summary>
        public static decimal DescuentoPorVolumen(int cantidad)
        {
            if (cantidad >= 100) return 0.20m;
            if (cantidad >= 50) return 0.15m;
            if (cantidad >= 10) return 0.10m;
            return 0m;
        }

        /// <summary>Catalogo de servicios adicionales contratables junto con el equipo.</summary>
        public static readonly IReadOnlyDictionary<string, (string Nombre, decimal Precio, string Detalle)> Servicios =
            new Dictionary<string, (string, decimal, string)>
            {
                ["soporte-premium"] = ("Soporte técnico premium", 49m, "Atención prioritaria 24/7 durante 12 meses."),
                ["capacitacion"] = ("Capacitación presencial", 149m, "Sesión de onboarding presencial para tu equipo."),
                ["api-access"] = ("Acceso a la API", 99m, "Credenciales de API REST para integrar las lecturas a tus sistemas."),
                ["personalizacion"] = ("Personalización de auras", 199m, "Paleta cromática y branding propios en la aplicación móvil.")
            };
    }
}
