using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CORSYNC.Core.Domain
{
    /// <summary>
    /// Producto comercial de ThinkUp. Actualmente la empresa comercializa un unico
    /// producto: la pulsera CORSYNC.
    /// </summary>
    public class Producto
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(120)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(300)]
        public string Descripcion { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string DescripcionLarga { get; set; } = string.Empty;

        /// <summary>Costo de mano de obra directa por unidad ensamblada.</summary>
        [Column(TypeName = "decimal(18,2)")]
        public decimal ManoObraUnitaria { get; set; }

        /// <summary>Porcentaje de gastos indirectos de fabricacion sobre el costo primo (0.25 = 25%).</summary>
        [Column(TypeName = "decimal(9,4)")]
        public decimal OverheadPorcentaje { get; set; } = 0.25m;

        /// <summary>Margen de utilidad aplicado sobre el costo total unitario (0.40 = 40%).</summary>
        [Column(TypeName = "decimal(9,4)")]
        public decimal MargenUtilidad { get; set; } = 0.40m;

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;

        public ICollection<RecetaProducto> Receta { get; set; } = new List<RecetaProducto>();
    }
}
