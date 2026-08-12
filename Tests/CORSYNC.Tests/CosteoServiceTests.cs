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

        // Ids del catalogo: 1 carcasa 3D, 2 MCU-6701, 3 MAX30102, 4 ESP32,
        // 5 bateria 9V, 6 indicador de carga, 7 regulador de voltaje,
        // 8 electrodos metal, 9 cables protoboard.
        private const int Esp32 = 4;
        private const int Max30102 = 3;

        [Fact]
        public async Task CalcularCostoProducto_ExplosionaLaRecetaCompleta()
        {
            using var context = GetDbContext();
            var servicio = new CosteoService(context);

            var costo = await servicio.CalcularCostoProductoAsync(1);

            Assert.NotNull(costo);
            Assert.Equal("CORSYNC", costo!.Producto);
            Assert.Equal(9, costo.Materiales.Count);
            // 100.00 + 259.96 + 64.24 + 129.99 + 150.00 + 80.00 + 95.60 + (2.50*2) + (1.50*20) = 914.79
            Assert.Equal(914.79m, costo.CostoMateriaPrima);
            Assert.Equal(974.79m, costo.CostoPrimo);      // + mano de obra 60.00
            Assert.Equal(243.70m, costo.CostoIndirecto);  // 25% del costo primo (243.6975 -> 243.70)
            Assert.Equal(1218.49m, costo.CostoUnitario);
            Assert.Equal(1827.74m, costo.PrecioLista);    // margen 50%
        }

        [Fact]
        public async Task CalcularCostoProducto_UnidadesFabricables_LimitadasPorElInsumoMasEscaso()
        {
            using var context = GetDbContext();

            var esp32 = context.MateriasPrimas.Single(m => m.Id == Esp32);
            esp32.Stock = 37;
            await context.SaveChangesAsync();

            var costo = await new CosteoService(context).CalcularCostoProductoAsync(1);

            Assert.NotNull(costo);
            Assert.Equal(37, costo!.UnidadesFabricables);
        }

        [Fact]
        public async Task CalcularCostoProducto_LaMermaAumentaElConsumoYElCosto()
        {
            using var context = GetDbContext();

            // 10% de merma sobre la carcasa impresa en 3D (100.00 por pieza).
            var renglon = context.RecetasProductos.Single(r => r.MateriaPrimaId == 1);
            renglon.MermaPorcentaje = 0.10m;
            await context.SaveChangesAsync();

            var costo = await new CosteoService(context).CalcularCostoProductoAsync(1);

            var carcasa = costo!.Materiales.Single(m => m.MateriaPrimaId == 1);
            Assert.Equal(1.1m, carcasa.CantidadConMerma);
            Assert.Equal(110.00m, carcasa.CostoTotal);        // 1.1 x 100.00
            Assert.Equal(924.79m, costo.CostoMateriaPrima);   // 914.79 - 100.00 + 110.00
        }

        [Fact]
        public async Task RegistrarEntradaInventario_PromediaElCostoEnProporcionALasCantidades()
        {
            using var context = GetDbContext();
            var servicio = new CosteoService(context);

            // Existencia inicial del MAX30102: 700 piezas a 64.24
            // Entrada: 300 piezas a 80.00
            // Promedio = (700*64.24 + 300*80) / 1000 = 68968 / 1000 = 68.968
            var impacto = await servicio.RegistrarEntradaInventarioAsync(Max30102, 300m, 80.00m);
            await context.SaveChangesAsync();

            Assert.NotNull(impacto);
            Assert.Equal(700m, impacto!.StockAnterior);
            Assert.Equal(64.24m, impacto.CostoAnterior);
            Assert.Equal(1000m, impacto.StockNuevo);
            Assert.Equal(68.968m, impacto.CostoPromedioNuevo);

            var insumo = await context.MateriasPrimas.FindAsync(Max30102);
            Assert.Equal(68.968m, insumo!.CostoUnidad);
        }

        /// <summary>
        /// El caso del enunciado del metodo: 9 ESP32 a 129.99 y despues 3 a 140.
        /// Saldo (9x129.99 + 3x140) = 1,589.91 entre 12 existencias = 132.4925.
        /// </summary>
        [Fact]
        public async Task RegistrarEntradaInventario_SegundaCompraAOtroPrecio_RecalculaElPromedio()
        {
            using var context = GetDbContext();

            var esp32 = context.MateriasPrimas.Single(m => m.Id == Esp32);
            esp32.Stock = 9m;
            esp32.CostoUnidad = 129.99m;
            await context.SaveChangesAsync();

            var servicio = new CosteoService(context);
            var impacto = await servicio.RegistrarEntradaInventarioAsync(Esp32, 3m, 140.00m);
            await context.SaveChangesAsync();

            Assert.NotNull(impacto);
            Assert.Equal(12m, impacto!.StockNuevo);
            Assert.Equal(132.4925m, impacto.CostoPromedioNuevo);
        }

        [Fact]
        public async Task RegistrarEntradaInventario_RepercuteEnElPrecioDelProducto()
        {
            using var context = GetDbContext();
            var servicio = new CosteoService(context);

            // ESP32: 520 a 129.99 + 520 a 170.01 -> promedio 150.00 (+20.01 por unidad).
            await servicio.RegistrarEntradaInventarioAsync(Esp32, 520m, 170.01m);
            await context.SaveChangesAsync();

            var esp32 = await context.MateriasPrimas.FindAsync(Esp32);
            Assert.Equal(150.00m, esp32!.CostoUnidad);

            var costo = await servicio.CalcularCostoProductoAsync(1);
            Assert.Equal(934.80m, costo!.CostoMateriaPrima);   // 914.79 + 20.01
            Assert.True(costo.PrecioLista > 1827.74m);
        }

        [Fact]
        public async Task RegistrarEntradaInventario_CantidadInvalida_NoAlteraElInventario()
        {
            using var context = GetDbContext();
            var servicio = new CosteoService(context);

            var impacto = await servicio.RegistrarEntradaInventarioAsync(Max30102, 0m, 15.00m);

            Assert.Null(impacto);
            var insumo = await context.MateriasPrimas.FindAsync(Max30102);
            Assert.Equal(64.24m, insumo!.CostoUnidad);
            Assert.Equal(700m, insumo.Stock);
        }

        // -----------------------------------------------------------------
        // Salidas: se valuan al ultimo promedio y no lo modifican.
        // -----------------------------------------------------------------

        /// <summary>
        /// El otro caso del enunciado: 10 unidades a 137 valen 1,370; al salir 2 se
        /// valuan en 274 y quedan 8 a 137, es decir 1,096 de saldo.
        /// </summary>
        [Fact]
        public async Task RegistrarSalidaInventario_UsaElUltimoPromedioYNoLoCambia()
        {
            using var context = GetDbContext();

            var esp32 = context.MateriasPrimas.Single(m => m.Id == Esp32);
            esp32.Stock = 10m;
            esp32.CostoUnidad = 137.00m;
            await context.SaveChangesAsync();

            var salida = await new CosteoService(context).RegistrarSalidaInventarioAsync(Esp32, 2m);
            await context.SaveChangesAsync();

            Assert.NotNull(salida);
            Assert.Equal(10m, salida!.StockAnterior);
            Assert.Equal(137.00m, salida.CostoPromedio);
            Assert.Equal(274.00m, salida.ImporteSalida);
            Assert.Equal(8m, salida.StockNuevo);
            Assert.Equal(1096.00m, salida.SaldoValorizado);

            // El costo por unidad sigue siendo el mismo despues de la salida.
            var despues = await context.MateriasPrimas.FindAsync(Esp32);
            Assert.Equal(137.00m, despues!.CostoUnidad);
        }

        [Fact]
        public async Task RegistrarSalidaInventario_SinExistenciasSuficientes_NoDescuenta()
        {
            using var context = GetDbContext();
            var servicio = new CosteoService(context);

            var salida = await servicio.RegistrarSalidaInventarioAsync(Esp32, 100000m);

            Assert.Null(salida);
            var insumo = await context.MateriasPrimas.FindAsync(Esp32);
            Assert.Equal(520m, insumo!.Stock);
        }

        [Fact]
        public async Task RegistrarConsumoProduccion_DescuentaLaRecetaAlCostoPromedio()
        {
            using var context = GetDbContext();
            var servicio = new CosteoService(context);

            var consumo = await servicio.RegistrarConsumoProduccionAsync(1, 3);
            await context.SaveChangesAsync();

            Assert.NotNull(consumo);
            Assert.True(consumo!.Aplicado);
            Assert.Empty(consumo.Faltantes);
            Assert.Equal(9, consumo.Salidas.Count);
            Assert.Equal(2744.37m, consumo.CostoMateriaPrimaConsumida); // 914.79 x 3

            var esp32 = await context.MateriasPrimas.FindAsync(Esp32);
            Assert.Equal(517m, esp32!.Stock);          // 520 - 3
            Assert.Equal(129.99m, esp32.CostoUnidad);  // el promedio no cambia
        }

        [Fact]
        public async Task RegistrarConsumoProduccion_SiFaltaUnInsumo_NoDescuentaNinguno()
        {
            using var context = GetDbContext();

            var esp32 = context.MateriasPrimas.Single(m => m.Id == Esp32);
            esp32.Stock = 2m;
            await context.SaveChangesAsync();

            var consumo = await new CosteoService(context).RegistrarConsumoProduccionAsync(1, 5);

            Assert.NotNull(consumo);
            Assert.False(consumo!.Aplicado);
            Assert.NotEmpty(consumo.Faltantes);
            Assert.Empty(consumo.Salidas);

            // Ningun insumo se movio, ni siquiera los que si alcanzaban.
            var max = await context.MateriasPrimas.FindAsync(Max30102);
            Assert.Equal(700m, max!.Stock);
            Assert.Equal(2m, esp32.Stock);
        }

        [Fact]
        public async Task RegistrarConsumoProduccion_ProductoInexistente_DevuelveNull()
        {
            using var context = GetDbContext();
            var consumo = await new CosteoService(context).RegistrarConsumoProduccionAsync(999, 1);
            Assert.Null(consumo);
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
