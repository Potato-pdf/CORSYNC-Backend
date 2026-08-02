using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CORSYNC.Core.Domain
{
    /// <summary>
    /// Solicitud de cotizacion de pulseras CORSYNC. Guarda tanto los datos del
    /// prospecto como el desglose del calculo, de modo que la cotizacion sea
    /// reproducible y auditable por el area comercial.
    /// </summary>
    public class Cotizacion
    {
        public int Id { get; set; }

        [MaxLength(50)]
        public string Folio { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string NombreCliente { get; set; } = string.Empty;

        [MaxLength(150)]
        public string Empresa { get; set; } = string.Empty;

        [Required]
        [MaxLength(120)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(40)]
        public string Telefono { get; set; } = string.Empty;

        [MaxLength(80)]
        public string Pais { get; set; } = string.Empty;

        [MaxLength(120)]
        public string NombreProducto { get; set; } = "Pulsera CORSYNC";

        public int ProductoId { get; set; } = 1;

        public int Cantidad { get; set; } = 1;

        /// <summary>"Individual", "Corporativa" o "Enterprise".</summary>
        [MaxLength(30)]
        public string TipoLicencia { get; set; } = "Individual";

        /// <summary>Claves de servicios adicionales separadas por coma.</summary>
        [MaxLength(400)]
        public string Servicios { get; set; } = string.Empty;

        [MaxLength(2000)]
        public string? Mensaje { get; set; }

        // --- Desglose del costeo ---

        /// <summary>Costo de materia prima por unidad segun la explosion de materiales.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoMateriaPrima { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoManoObra { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoIndirecto { get; set; }

        /// <summary>Costo unitario de produccion (materia prima + mano de obra + indirectos).</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoUnitario { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioUnitario { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [Column(TypeName = "decimal(9,4)")]
        public decimal DescuentoPorcentaje { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DescuentoMonto { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal TotalServicios { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Impuestos { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal CostoTotal { get; set; }

        /// <summary>"Nueva", "Contactado" o "Cerrada".</summary>
        [MaxLength(20)]
        public string Estado { get; set; } = "Nueva";

        public DateTime FechaCotizacion { get; set; } = DateTime.UtcNow;

        public DateTime FechaVigencia { get; set; } = DateTime.UtcNow.AddDays(30);
    }
}
