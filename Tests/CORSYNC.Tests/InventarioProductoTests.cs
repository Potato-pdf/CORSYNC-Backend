using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CORSYNC.Api.Controllers;
using CORSYNC.Core.Domain;
using CORSYNC.Core.DTOs;
using CORSYNC.Core.Interfaces;
using CORSYNC.Infrastructure.Costing;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Tests
{
    /// <summary>
    /// Ciclo de inventario completo: la compra a proveedor llena el almacen de materia
    /// prima, la produccion lo consume y genera producto terminado, y la venta descuenta
    /// ese producto terminado. Antes de esto el stock solo subia y nunca bajaba.
    /// </summary>
    public class InventarioProductoTests
    {
        private const int ProductoId = 1;

        private static AdminDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AdminDbContext>()
                .UseInMemoryDatabase(databaseName: $"CORSYNC_Inventario_Test_{Guid.NewGuid()}")
                .Options;

            var context = new AdminDbContext(options);
            context.Database.EnsureCreated();
            return context;
        }

        private static ClienteController GetClienteController(AdminDbContext context) =>
            new ClienteController(context, new CORSYNC.Infrastructure.Auth.AuthService(
                new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()));

        private static CompraClienteRequest VentaDe(int cantidad) => new CompraClienteRequest
        {
            UsuarioId = 2,          // cliente sembrado
            ProductoId = ProductoId,
            Cantidad = cantidad,
            Monto = 1000m,
            Estado = "Procesando"
        };

        private static async Task<int> StockAsync(AdminDbContext context) =>
            (await context.Productos.FirstAsync(p => p.Id == ProductoId)).Stock;

        // -----------------------------------------------------------------
        // Produccion: insumos -> producto terminado
        // -----------------------------------------------------------------

        [Fact]
        public async Task Produccion_SumaLasUnidadesFabricadasAlStockDelProducto()
        {
            using var context = GetDbContext();
            var servicio = new CosteoService(context);

            int antes = await StockAsync(context);
            var consumo = await servicio.RegistrarConsumoProduccionAsync(ProductoId, 4);
            await context.SaveChangesAsync();

            Assert.NotNull(consumo);
            Assert.True(consumo!.Aplicado);
            Assert.Equal(antes, consumo.StockProductoAnterior);
            Assert.Equal(antes + 4, consumo.StockProductoNuevo);
            Assert.Equal(antes + 4, await StockAsync(context));
        }

        [Fact]
        public async Task Produccion_TambienDescuentaLaMateriaPrima()
        {
            using var context = GetDbContext();
            var servicio = new CosteoService(context);

            var receta = await context.RecetasProductos
                .Include(r => r.MateriaPrima)
                .Where(r => r.ProductoId == ProductoId)
                .ToListAsync();
            var primerInsumo = receta.First();
            decimal stockInsumoAntes = primerInsumo.MateriaPrima!.Stock;

            await servicio.RegistrarConsumoProduccionAsync(ProductoId, 2);
            await context.SaveChangesAsync();

            decimal stockInsumoDespues = (await context.MateriasPrimas
                .FirstAsync(m => m.Id == primerInsumo.MateriaPrimaId)).Stock;

            Assert.True(stockInsumoDespues < stockInsumoAntes,
                "fabricar debe consumir insumos del almacén de materia prima");
        }

        [Fact]
        public async Task Produccion_SinInventarioSuficiente_NoTocaNingunAlmacen()
        {
            using var context = GetDbContext();
            var servicio = new CosteoService(context);

            int stockAntes = await StockAsync(context);

            // Mas unidades de las que alcanza la materia prima sembrada.
            var consumo = await servicio.RegistrarConsumoProduccionAsync(ProductoId, 1_000_000);
            await context.SaveChangesAsync();

            Assert.NotNull(consumo);
            Assert.False(consumo!.Aplicado);
            Assert.NotEmpty(consumo.Faltantes);
            Assert.Equal(stockAntes, await StockAsync(context));
        }

        // -----------------------------------------------------------------
        // Venta: producto terminado -> cliente
        // -----------------------------------------------------------------

        [Fact]
        public async Task Venta_DescuentaDelStockDeProductoTerminado()
        {
            using var context = GetDbContext();
            await new CosteoService(context).RegistrarConsumoProduccionAsync(ProductoId, 10);
            await context.SaveChangesAsync();
            int antes = await StockAsync(context);

            var resultado = await GetClienteController(context).RegistrarCompra(VentaDe(3));

            Assert.IsType<OkObjectResult>(resultado);
            Assert.Equal(antes - 3, await StockAsync(context));
        }

        [Fact]
        public async Task Venta_SinStockSuficiente_SeRechazaYNoDescuenta()
        {
            using var context = GetDbContext();
            await new CosteoService(context).RegistrarConsumoProduccionAsync(ProductoId, 2);
            await context.SaveChangesAsync();
            int antes = await StockAsync(context);

            var resultado = await GetClienteController(context).RegistrarCompra(VentaDe(antes + 1));

            Assert.IsType<BadRequestObjectResult>(resultado);
            Assert.Equal(antes, await StockAsync(context));
            Assert.Empty(context.ComprasClientes.Where(c => c.Cantidad == antes + 1));
        }

        [Fact]
        public async Task Venta_ConElAlmacenVacio_SeRechaza()
        {
            using var context = GetDbContext();

            // El seed deja un lote inicial; se agota a proposito para comprobar que
            // con el almacen en cero no se puede vender nada.
            var producto = await context.Productos.FirstAsync(p => p.Id == ProductoId);
            producto.Stock = 0;
            await context.SaveChangesAsync();

            var resultado = await GetClienteController(context).RegistrarCompra(VentaDe(1));

            Assert.IsType<BadRequestObjectResult>(resultado);
            Assert.Equal(0, await StockAsync(context));
        }

        [Fact]
        public async Task Seed_DejaElAlmacenCuadradoConLaVentaDeDemostracion()
        {
            using var context = GetDbContext();

            // 25 fabricadas menos la unidad de la venta sembrada.
            var demo = await context.ComprasClientes.FirstOrDefaultAsync(c => c.Folio == "VTA-2026-0001");

            Assert.NotNull(demo);
            Assert.Equal(25 - demo!.Cantidad, await StockAsync(context));
        }

        // -----------------------------------------------------------------
        // Devoluciones al almacen
        // -----------------------------------------------------------------

        [Fact]
        public async Task CancelarVenta_DevuelveLasUnidadesAlAlmacen()
        {
            using var context = GetDbContext();
            var controller = GetClienteController(context);
            await new CosteoService(context).RegistrarConsumoProduccionAsync(ProductoId, 10);
            await context.SaveChangesAsync();

            var ok = Assert.IsType<OkObjectResult>(await controller.RegistrarCompra(VentaDe(4)));
            var compra = Assert.IsType<CompraCliente>(ok.Value);
            int trasVender = await StockAsync(context);

            await controller.ActualizarEstadoCompra(compra.Id, new CambioEstadoRequest { Estado = "Cancelado" });

            Assert.Equal(trasVender + 4, await StockAsync(context));
        }

        [Fact]
        public async Task ReactivarVentaCancelada_VuelveATomarLasUnidades()
        {
            using var context = GetDbContext();
            var controller = GetClienteController(context);
            await new CosteoService(context).RegistrarConsumoProduccionAsync(ProductoId, 10);
            await context.SaveChangesAsync();

            var ok = Assert.IsType<OkObjectResult>(await controller.RegistrarCompra(VentaDe(4)));
            var compra = Assert.IsType<CompraCliente>(ok.Value);

            await controller.ActualizarEstadoCompra(compra.Id, new CambioEstadoRequest { Estado = "Cancelado" });
            int trasCancelar = await StockAsync(context);

            await controller.ActualizarEstadoCompra(compra.Id, new CambioEstadoRequest { Estado = "Procesando" });

            Assert.Equal(trasCancelar - 4, await StockAsync(context));
        }

        [Fact]
        public async Task CancelarDosVeces_NoDevuelveLasUnidadesPorDuplicado()
        {
            using var context = GetDbContext();
            var controller = GetClienteController(context);
            await new CosteoService(context).RegistrarConsumoProduccionAsync(ProductoId, 10);
            await context.SaveChangesAsync();

            var ok = Assert.IsType<OkObjectResult>(await controller.RegistrarCompra(VentaDe(4)));
            var compra = Assert.IsType<CompraCliente>(ok.Value);

            await controller.ActualizarEstadoCompra(compra.Id, new CambioEstadoRequest { Estado = "Cancelado" });
            int trasPrimera = await StockAsync(context);

            await controller.ActualizarEstadoCompra(compra.Id, new CambioEstadoRequest { Estado = "Cancelado" });

            Assert.Equal(trasPrimera, await StockAsync(context));
        }

        [Fact]
        public async Task CambioDeEstadoNormal_NoAlteraElAlmacen()
        {
            using var context = GetDbContext();
            var controller = GetClienteController(context);
            await new CosteoService(context).RegistrarConsumoProduccionAsync(ProductoId, 10);
            await context.SaveChangesAsync();

            var ok = Assert.IsType<OkObjectResult>(await controller.RegistrarCompra(VentaDe(4)));
            var compra = Assert.IsType<CompraCliente>(ok.Value);
            int trasVender = await StockAsync(context);

            await controller.ActualizarEstadoCompra(compra.Id, new CambioEstadoRequest { Estado = "Enviado" });
            await controller.ActualizarEstadoCompra(compra.Id, new CambioEstadoRequest { Estado = "Entregado" });

            Assert.Equal(trasVender, await StockAsync(context));
        }

        [Fact]
        public async Task EliminarVentaActiva_DevuelveLasUnidades()
        {
            using var context = GetDbContext();
            var controller = GetClienteController(context);
            await new CosteoService(context).RegistrarConsumoProduccionAsync(ProductoId, 10);
            await context.SaveChangesAsync();

            var ok = Assert.IsType<OkObjectResult>(await controller.RegistrarCompra(VentaDe(4)));
            var compra = Assert.IsType<CompraCliente>(ok.Value);
            int trasVender = await StockAsync(context);

            await controller.EliminarCompra(compra.Id);

            Assert.Equal(trasVender + 4, await StockAsync(context));
        }

        [Fact]
        public async Task EliminarVentaYaCancelada_NoDevuelveLasUnidadesDosVeces()
        {
            using var context = GetDbContext();
            var controller = GetClienteController(context);
            await new CosteoService(context).RegistrarConsumoProduccionAsync(ProductoId, 10);
            await context.SaveChangesAsync();

            var ok = Assert.IsType<OkObjectResult>(await controller.RegistrarCompra(VentaDe(4)));
            var compra = Assert.IsType<CompraCliente>(ok.Value);

            await controller.ActualizarEstadoCompra(compra.Id, new CambioEstadoRequest { Estado = "Cancelado" });
            int trasCancelar = await StockAsync(context);

            await controller.EliminarCompra(compra.Id);

            Assert.Equal(trasCancelar, await StockAsync(context));
        }

        // -----------------------------------------------------------------
        // Ciclo completo
        // -----------------------------------------------------------------

        [Fact]
        public async Task CicloCompleto_FabricarYVenderDejaElAlmacenCuadrado()
        {
            using var context = GetDbContext();
            var controller = GetClienteController(context);

            int inicial = await StockAsync(context);

            await new CosteoService(context).RegistrarConsumoProduccionAsync(ProductoId, 12);
            await context.SaveChangesAsync();
            Assert.Equal(inicial + 12, await StockAsync(context));

            await controller.RegistrarCompra(VentaDe(5));
            await controller.RegistrarCompra(VentaDe(3));

            // 12 fabricadas - 8 vendidas = 4 disponibles
            Assert.Equal(inicial + 4, await StockAsync(context));
        }

        /// <summary>
        /// El bootstrapper de SQL reaplicaba en cada arranque los valores de arranque
        /// de la materia prima, incluidos Stock y CostoUnidad, con lo que un reinicio
        /// borraba las recepciones, la produccion y el costo promedio acumulado. Aqui
        /// se comprueba el invariante desde el lado del dominio: las operaciones dejan
        /// el inventario en un estado que nada mas deberia sobrescribir.
        /// </summary>
        [Fact]
        public async Task OperarElInventario_DejaValoresQueNoCoincidenConLosDeArranque()
        {
            using var context = GetDbContext();

            var insumo = await context.MateriasPrimas.FirstAsync(m => m.Id == 4); // ESP32
            decimal stockArranque = insumo.Stock;
            decimal costoArranque = insumo.CostoUnidad;

            // Una recepcion mueve el costo promedio, y fabricar mueve las existencias.
            await new CosteoService(context).RegistrarEntradaInventarioAsync(4, 100m, costoArranque * 2);
            await new CosteoService(context).RegistrarConsumoProduccionAsync(ProductoId, 5);
            await context.SaveChangesAsync();

            var despues = await context.MateriasPrimas.FirstAsync(m => m.Id == 4);

            Assert.NotEqual(stockArranque, despues.Stock);
            Assert.NotEqual(costoArranque, despues.CostoUnidad);
        }

        [Fact]
        public async Task Cotizar_NoMueveNingunAlmacen()
        {
            using var context = GetDbContext();
            var cotizacion = new CotizacionController(context, new CosteoService(context));

            int stockAntes = await StockAsync(context);
            var insumosAntes = await context.MateriasPrimas
                .OrderBy(m => m.Id).Select(m => m.Stock).ToListAsync();

            await cotizacion.CalcularCotizacion(new CotizacionRequest
            {
                NombreCliente = "Interesado",
                Email = "interesado@ejemplo.com",
                Telefono = "3312345678",
                Cantidad = 50,
                TipoLicencia = "Individual",
                AceptaPrivacidad = true
            });

            var insumosDespues = await context.MateriasPrimas
                .OrderBy(m => m.Id).Select(m => m.Stock).ToListAsync();

            Assert.Equal(stockAntes, await StockAsync(context));
            Assert.Equal(insumosAntes, insumosDespues);
        }
    }
}
