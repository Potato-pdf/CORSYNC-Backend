using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CORSYNC.Core.Domain
{
    /// <summary>
    /// Insumo utilizado en la fabricacion de la pulsera CORSYNC. El campo
    /// <see cref="CostoUnidad"/> guarda el costo promedio ponderado vigente, que se
    /// recalcula cada vez que se recibe una compra a proveedor.
    /// </summary>
    public class MateriaPrima
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(150)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(400)]
        public string Descripcion { get; set; } = string.Empty;

        /// <summary>Costo promedio ponderado por unidad de medida.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal CostoUnidad { get; set; }

        [MaxLength(30)]
        public string UnidadMedida { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,4)")]
        public decimal Stock { get; set; }

        /// <summary>Nivel de stock por debajo del cual se sugiere reabastecer.</summary>
        [Column(TypeName = "decimal(18,4)")]
        public decimal StockMinimo { get; set; }

        public int? ProveedorId { get; set; }
        public Proveedor? Proveedor { get; set; }

        public bool Activo { get; set; } = true;
    }
}
