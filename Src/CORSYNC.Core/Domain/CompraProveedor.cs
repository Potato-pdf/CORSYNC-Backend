using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CORSYNC.Core.Domain
{
    /// <summary>
    /// Orden de compra de materia prima a un proveedor. Al marcarse como recibida
    /// alimenta el inventario y recalcula el costo promedio ponderado de cada insumo.
    /// </summary>
    public class CompraProveedor
    {
        public int Id { get; set; }

        [Required]
        public int ProveedorId { get; set; }
        public Proveedor? Proveedor { get; set; }

        [MaxLength(50)]
        public string Folio { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,2)")]
        public decimal MontoTotal { get; set; }

        /// <summary>"Pendiente", "Recibida" o "Cancelada".</summary>
        [Required]
        [MaxLength(20)]
        public string Estado { get; set; } = "Pendiente";

        [MaxLength(500)]
        public string? Notas { get; set; }

        public DateTime FechaCompra { get; set; } = DateTime.UtcNow;

        public DateTime? FechaRecepcion { get; set; }

        public ICollection<DetalleCompraProveedor> Detalles { get; set; } = new List<DetalleCompraProveedor>();
    }

    public class DetalleCompraProveedor
    {
        public int Id { get; set; }

        [Required]
        public int CompraProveedorId { get; set; }
        public CompraProveedor? CompraProveedor { get; set; }

        [Required]
        public int MateriaPrimaId { get; set; }
        public MateriaPrima? MateriaPrima { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal Cantidad { get; set; }

        /// <summary>Costo unitario pactado con el proveedor en esta compra.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal CostoUnitario { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Importe { get; set; }
    }
}
