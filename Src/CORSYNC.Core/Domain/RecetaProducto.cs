using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CORSYNC.Core.Domain
{
    /// <summary>
    /// Renglon de la explosion de materiales (BOM) de un producto: que insumo se
    /// consume y en que cantidad por unidad fabricada.
    /// </summary>
    public class RecetaProducto
    {
        public int Id { get; set; }

        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }

        /// <summary>Se conserva por compatibilidad con las recetas cargadas previamente.</summary>
        [MaxLength(120)]
        public string NombreProducto { get; set; } = string.Empty;

        public int MateriaPrimaId { get; set; }
        public MateriaPrima? MateriaPrima { get; set; }

        [Column(TypeName = "decimal(18,4)")]
        public decimal CantidadRequerida { get; set; }

        /// <summary>Porcentaje de merma esperado en el proceso (0.05 = 5%).</summary>
        [Column(TypeName = "decimal(9,4)")]
        public decimal MermaPorcentaje { get; set; }
    }
}
