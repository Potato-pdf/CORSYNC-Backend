using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Xunit;
using CORSYNC.Api.Controllers;
using CORSYNC.Core.DTOs;
using CORSYNC.Infrastructure.Costing;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Tests
{
    /// <summary>
    /// El precio de la pulsera CORSYNC se deriva del metodo de costeo de la empresa,
    /// no de un valor fijo. Estas pruebas fijan el encadenamiento completo:
    /// explosion de materiales -> costo unitario -> precio de lista -> total cotizado.
    /// </summary>
    public class CotizacionControllerTests
    {
        // Explosion de materiales sembrada: 3.10 + 9.80 + 6.50 + 8.00 + 12.00
        //                                 + 4.20 + 2.40 + 7.60 + 5.30 + 2.90 = 61.80
        private const decimal CostoMateriaPrima = 61.80m;
        private const decimal ManoObra = 18.20m;          // costo primo = 80.00
        private const decimal CostoIndirecto = 20.00m;    // 25% del costo primo
        private const decimal CostoUnitario = 100.00m;
        private const decimal PrecioLista = 299.00m;      // margen 199%

        private static AdminDbContext GetDbContext()
        {
            var options = new DbContextOptionsBuilder<AdminDbContext>()
                .UseInMemoryDatabase(databaseName: $"CORSYNC_Admin_Test_{System.Guid.NewGuid()}")
                .Options;

            var context = new AdminDbContext(options);
            context.Database.EnsureCreated(); // Siembra producto, insumos y receta
            return context;
        }

        private static CotizacionController GetController(AdminDbContext context) =>
            new CotizacionController(context, new CosteoService(context));

        private static CotizacionRequest RequestBase() => new CotizacionRequest
        {
            NombreCliente = "Cliente Prueba",
            Email = "cliente@prueba.com",
            Telefono = "+52 33 0000 0000",
            Cantidad = 1,
            TipoLicencia = "Individual",
            AceptaPrivacidad = true
        };

        [Fact]
        public async Task CalcularCotizacion_UnaUnidadIndividual_DerivaElPrecioDelCosteo()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            var actionResult = await controller.CalcularCotizacion(RequestBase());

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var cotizacion = Assert.IsType<CotizacionResponse>(okResult.Value);

            Assert.Equal(CostoMateriaPrima, cotizacion.CostoMateriaPrima);
            Assert.Equal(ManoObra, cotizacion.CostoManoObra);
            Assert.Equal(CostoIndirecto, cotizacion.CostoIndirecto);
            Assert.Equal(CostoUnitario, cotizacion.CostoUnitario);
            Assert.Equal(PrecioLista, cotizacion.PrecioLista);
            Assert.Equal(PrecioLista, cotizacion.PrecioUnitario);

            // Sin descuento por volumen y sin servicios: total = 299 + 16% IVA
            Assert.Equal(0m, cotizacion.DescuentoPorcentaje);
            Assert.Equal(299.00m, cotizacion.Subtotal);
            Assert.Equal(47.84m, cotizacion.Impuestos);
            Assert.Equal(346.84m, cotizacion.Total);
        }

        [Fact]
        public async Task CalcularCotizacion_DiezUnidades_AplicaDescuentoPorVolumen()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            var request = RequestBase();
            request.Cantidad = 10;

            var actionResult = await controller.CalcularCotizacion(request);

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var cotizacion = Assert.IsType<CotizacionResponse>(okResult.Value);

            // 299.00 x 10 = 2990.00, descuento 10% = 299.00, base = 2691.00
            Assert.Equal(0.10m, cotizacion.DescuentoPorcentaje);
            Assert.Equal(2990.00m, cotizacion.Subtotal);
            Assert.Equal(299.00m, cotizacion.DescuentoMonto);
            Assert.Equal(430.56m, cotizacion.Impuestos);
            Assert.Equal(3121.56m, cotizacion.Total);
        }

        [Fact]
        public async Task CalcularCotizacion_LicenciaEnterpriseConServicios_AjustaPrecioYSuma()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            var request = RequestBase();
            request.Cantidad = 100;
            request.TipoLicencia = "Enterprise";
            request.Servicios = new List<string> { "soporte-premium", "api-access" };

            var actionResult = await controller.CalcularCotizacion(request);

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var cotizacion = Assert.IsType<CotizacionResponse>(okResult.Value);

            // Enterprise: 299.00 x 0.83 = 248.17 por unidad
            Assert.Equal(248.17m, cotizacion.PrecioUnitario);
            Assert.Equal(24817.00m, cotizacion.Subtotal);

            // 100 unidades: 20% de descuento
            Assert.Equal(0.20m, cotizacion.DescuentoPorcentaje);
            Assert.Equal(4963.40m, cotizacion.DescuentoMonto);

            // Servicios: soporte premium 49 + API 99 = 148
            Assert.Equal(2, cotizacion.Servicios.Count);
            Assert.Equal(148m, cotizacion.TotalServicios);

            // Base = 24817.00 - 4963.40 + 148.00 = 20001.60
            Assert.Equal(3200.26m, cotizacion.Impuestos);
            Assert.Equal(23201.86m, cotizacion.Total);
        }

        [Fact]
        public async Task CalcularCotizacion_ServicioDesconocido_SeIgnora()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            var request = RequestBase();
            request.Servicios = new List<string> { "servicio-inexistente" };

            var actionResult = await controller.CalcularCotizacion(request);

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var cotizacion = Assert.IsType<CotizacionResponse>(okResult.Value);

            Assert.Empty(cotizacion.Servicios);
            Assert.Equal(0m, cotizacion.TotalServicios);
        }

        [Fact]
        public async Task CalcularCotizacion_SinAceptarPrivacidad_ReturnsBadRequest()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            var request = RequestBase();
            request.AceptaPrivacidad = false;

            var actionResult = await controller.CalcularCotizacion(request);

            Assert.IsType<BadRequestObjectResult>(actionResult);
        }

        [Fact]
        public async Task CalcularCotizacion_PersisteLaSolicitudConFolio()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            await controller.CalcularCotizacion(RequestBase());

            var guardada = Assert.Single(context.Cotizaciones.ToList());
            Assert.Equal("Cliente Prueba", guardada.NombreCliente);
            Assert.Equal("Nueva", guardada.Estado);
            Assert.StartsWith("COT-", guardada.Folio);
            Assert.Equal(346.84m, guardada.CostoTotal);
        }
    }
}
