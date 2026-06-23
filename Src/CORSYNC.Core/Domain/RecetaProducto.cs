namespace CORSYNC.Core.Domain
{
    public class RecetaProducto
    {
        public int Id { get; set; }
        public string NombreProducto { get; set; } = string.Empty;
        public int MateriaPrimaId { get; set; }
        public MateriaPrima? MateriaPrima { get; set; }
        public decimal CantidadRequerida { get; set; }
    }
}
