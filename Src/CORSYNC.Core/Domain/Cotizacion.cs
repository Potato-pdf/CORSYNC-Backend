using System;

namespace CORSYNC.Core.Domain
{
    public class Cotizacion
    {
        public int Id { get; set; }
        public string NombreCliente { get; set; } = string.Empty;
        public string NombreProducto { get; set; } = string.Empty;
        public decimal Ancho { get; set; }
        public decimal Alto { get; set; }
        public decimal CostoTotal { get; set; }
        public DateTime FechaCotizacion { get; set; } = DateTime.UtcNow;
    }
}
