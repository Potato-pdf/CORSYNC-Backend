using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CORSYNC.Core.Domain;
using CORSYNC.Infrastructure.Costing;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Tests
{
    /// <summary>
    /// Metodo de costeo de ThinkUp: costo promedio ponderado para la materia prima y
    /// costeo absorbente (materia prima + mano de obra + gastos indirectos) para el
    /// producto terminado.
    /// </summary>
    public class CosteoServiceTests
    {
        private static AdminDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AdminDbContext>()
                .UseInMemoryDatabase(databaseName: $"CORSYNC_Costeo_Test_{System.Guid.NewGuid()}")
                .Options;

            var context = new AdminDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        [Fact]
        public async Task CalcularCostoProducto_ExplosionaLaRecetaCompleta()
        {
            using var context = GetDbContext();
            var servicio = new CosteoService(context);

            var costo = await servicio.CalcularCostoProductoAsync(1);

            Assert.NotNull(costo);
            Assert.Equal("CORSYNC", costo!.Producto);
            Assert.Equal(10, costo.Materiales.Count);
            Assert.Equal(61.80m, costo.CostoMateriaPrima);
            Assert.Equal(80.00m, costo.CostoPrimo);
            Assert.Equal(20.00m, costo.CostoIndirecto);
            Assert.Equal(100.00m, costo.CostoUnitario);
            Assert.Equal(299.00m, costo.PrecioLista);
        }

        [Fact]
        public async Task CalcularCostoProducto_UnidadesFabricables_LimitadasPorElInsumoMasEscaso()
        {
            using var context = GetDbContext();

            // El PCB flexible es el insumo con menos existencias (450 piezas).
            var pcb = context.MateriasPrimas.Single(m => m.Nombre == "PCB flexible de 4 capas");
            pcb.Stock = 37;
            await context.SaveChangesAsync();

            var costo = await new CosteoService(context).CalcularCostoProductoAsync(1);

            Assert.NotNull(costo);
            Assert.Equal(37, costo!.UnidadesFabricables);
        }

        [Fact]
        public async Task CalcularCostoProducto_LaMermaAumentaElConsumoYElCosto()
        {
            using var context = GetDbContext();

            // 10% de merma sobre la correa de silicona (3.10 por pieza).
            var renglon = context.RecetasProductos.Single(r => r.MateriaPrimaId == 1);
            renglon.MermaPorcentaje = 0.10m;
            await context.SaveChangesAsync();

            var costo = await new CosteoService(context).CalcularCostoProductoAsync(1);

            var correa = costo!.Materiales.Single(m => m.MateriaPrimaId == 1);
            Assert.Equal(1.1m, correa.CantidadConMerma);
            Assert.Equal(3.41m, correa.CostoTotal);          // 1.1 x 3.10
            Assert.Equal(62.11m, costo.CostoMateriaPrima);   // 61.80 - 3.10 + 3.41
        }

        [Fact]
        public async Task RegistrarEntradaInventario_PromediaElCostoEnProporcionALasCantidades()
        {
            using var context = GetDbContext();
            var servicio = new CosteoService(context);

            // Existencia inicial del sensor MAX30102: 700 piezas a 8.00
            // Entrada: 300 piezas a 12.00
            // Promedio ponderado = (700*8 + 300*12) / 1000 = 9200 / 1000 = 9.20
            var impacto = await servicio.RegistrarEntradaInventarioAsync(4, 300m, 12.00m);
            await context.SaveChangesAsync();

            Assert.NotNull(impacto);
            Assert.Equal(700m, impacto!.StockAnterior);
            Assert.Equal(8.00m, impacto.CostoAnterior);
            Assert.Equal(1000m, impacto.StockNuevo);
            Assert.Equal(9.20m, impacto.CostoPromedioNuevo);

            var insumo = await context.MateriasPrimas.FindAsync(4);
            Assert.Equal(9.20m, insumo!.CostoUnidad);
        }

        [Fact]
        public async Task RegistrarEntradaInventario_RepercuteEnElPrecioDelProducto()
        {
            using var context = GetDbContext();
            var servicio = new CosteoService(context);

            // Encarecer un insumo debe subir el costo unitario y el precio de lista.
            await servicio.RegistrarEntradaInventarioAsync(5, 520m, 20.00m); // ESP32: 520 a 12.00 + 520 a 20.00
            await context.SaveChangesAsync();

            var esp32 = await context.MateriasPrimas.FindAsync(5);
            Assert.Equal(16.00m, esp32!.CostoUnidad); // promedio de 12 y 20 con cantidades iguales

            var costo = await servicio.CalcularCostoProductoAsync(1);
            Assert.Equal(65.80m, costo!.CostoMateriaPrima);   // 61.80 + 4.00
            Assert.True(costo.PrecioLista > 299.00m);
        }

        [Fact]
        public async Task RegistrarEntradaInventario_CantidadInvalida_NoAlteraElInventario()
        {
            using var context = GetDbContext();
            var servicio = new CosteoService(context);

            var impacto = await servicio.RegistrarEntradaInventarioAsync(4, 0m, 15.00m);

            Assert.Null(impacto);
            var insumo = await context.MateriasPrimas.FindAsync(4);
            Assert.Equal(8.00m, insumo!.CostoUnidad);
            Assert.Equal(700m, insumo.Stock);
        }

        [Fact]
        public async Task CalcularCostoProducto_ProductoInexistente_DevuelveNull()
        {
            using var context = GetDbContext();
            var costo = await new CosteoService(context).CalcularCostoProductoAsync(999);
            Assert.Null(costo);
        }
    }
}
