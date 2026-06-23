namespace CORSYNC.Core.Domain
{
    public class MateriaPrima
    {
        public int Id { get; set; }
        public string Nombre { get; set; } = string.Empty;
        public decimal CostoUnidad { get; set; }
        public string UnidadMedida { get; set; } = string.Empty;
        public decimal Stock { get; set; }
    }
}
