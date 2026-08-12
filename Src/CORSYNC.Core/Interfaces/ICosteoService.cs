using System;
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

        /// <summary>
        /// Descuenta existencias valuandolas al ultimo costo promedio calculado. La
        /// salida no altera el promedio: no importa de que compra salieron las
        /// unidades, todas valen el promedio vigente. Devuelve null si el insumo no
        /// existe, la cantidad no es positiva o no hay existencias suficientes.
        /// </summary>
        Task<SalidaCosteoResponse?> RegistrarSalidaInventarioAsync(int materiaPrimaId, decimal cantidad);

        /// <summary>
        /// Explosiona la receta y descuenta del inventario lo necesario para fabricar
        /// las unidades pedidas, valuando cada salida al costo promedio vigente. Es
        /// todo o nada: si falta un solo insumo no se descuenta ninguno.
        /// </summary>
        Task<ConsumoProduccionResponse?> RegistrarConsumoProduccionAsync(int productoId, int unidades);
    }

    /// <summary>Reglas comerciales de precio publicadas por el area de ventas de ThinkUp.</summary>
    public static class ReglasComerciales
    {
        public const decimal TasaImpuesto = 0.16m;

        /// <summary>Cantidad maxima de unidades admitida en una cotizacion.</summary>
        public const int CantidadMaxima = 100;

        /// <summary>
        /// Catalogo de licencias. Es la unica fuente de verdad: tanto el calculo
        /// como el endpoint de parametros que alimenta el formulario publico leen
        /// de aqui, para que el precio anunciado no pueda separarse del cobrado.
        /// </summary>
        public static readonly IReadOnlyList<(string Clave, string Nombre, decimal Factor, string Descripcion)> Licencias =
            new[]
            {
                ("Individual", "Individual", 1.00m, "Para uso personal. Una manga y una cuenta en la app."),
                ("Corporativa", "Corporativa", 0.90m, "Para programas de bienestar. Panel de equipo y facturación."),
                ("Enterprise", "Enterprise", 0.83m, "Para distribuidores y despliegues grandes. Precio de mayoreo.")
            };

        /// <summary>Factor de precio segun el tipo de licencia contratada.</summary>
        public static decimal FactorLicencia(string? tipoLicencia)
        {
            var normalizada = (tipoLicencia ?? string.Empty).Trim();
            foreach (var licencia in Licencias)
            {
                if (string.Equals(licencia.Clave, normalizada, StringComparison.OrdinalIgnoreCase))
                {
                    return licencia.Factor;
                }
            }
            return 1.00m;
        }

        public static string NormalizarLicencia(string? tipoLicencia)
        {
            var normalizada = (tipoLicencia ?? string.Empty).Trim();
            foreach (var licencia in Licencias)
            {
                if (string.Equals(licencia.Clave, normalizada, StringComparison.OrdinalIgnoreCase))
                {
                    return licencia.Clave;
                }
            }
            return "Individual";
        }

        /// <summary>
        /// Tramos de descuento por volumen, de menor a mayor. Igual que las
        /// licencias, se publican y se aplican desde esta misma lista.
        /// </summary>
        public static readonly IReadOnlyList<(int Desde, decimal Porcentaje)> TramosVolumen =
            new[] { (5, 0.10m), (15, 0.15m) };

        /// <summary>Descuento por volumen aplicado sobre el subtotal.</summary>
        public static decimal DescuentoPorVolumen(int cantidad)
        {
            decimal descuento = 0m;
            foreach (var tramo in TramosVolumen)
            {
                if (cantidad >= tramo.Desde && tramo.Porcentaje > descuento)
                {
                    descuento = tramo.Porcentaje;
                }
            }
            return descuento;
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
