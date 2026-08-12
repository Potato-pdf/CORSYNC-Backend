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
        // Explosion de materiales sembrada: 100.00 + 259.96 + 64.24 + 129.99
        //                                  + 150.00 + 80.00 + 95.60 + (2.50*2) + (1.50*20) = 914.79
        private const decimal CostoMateriaPrima = 914.79m;
        private const decimal ManoObra = 60.00m;             // costo primo = 974.79
        private const decimal CostoIndirecto = 243.6975m;    // 25% del costo primo
        private const decimal CostoUnitario = 1218.4875m;
        // Margen 40% sobre el costo unitario = 487.395 de utilidad, la misma cifra
        // que el 50% sobre el costo primo de la hoja de costeo.
        private const decimal PrecioLista = 1705.8825m;

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
            Telefono = "3300000000",
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
            // El precio que se cobra si se redondea a centavos: 1,705.8825 -> 1,705.88
            Assert.Equal(1705.88m, cotizacion.PrecioUnitario);

            // Sin descuento por volumen y sin servicios: total = 1,705.88 + 16% IVA
            Assert.Equal(0m, cotizacion.DescuentoPorcentaje);
            Assert.Equal(1705.88m, cotizacion.Subtotal);
            Assert.Equal(272.94m, cotizacion.Impuestos);
            Assert.Equal(1978.82m, cotizacion.Total);
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

            // 1,705.88 x 10 = 17,058.80, descuento 10% = 1,705.88, base = 15,352.92
            Assert.Equal(0.10m, cotizacion.DescuentoPorcentaje);
            Assert.Equal(17058.80m, cotizacion.Subtotal);
            Assert.Equal(1705.88m, cotizacion.DescuentoMonto);
            Assert.Equal(2456.47m, cotizacion.Impuestos);
            Assert.Equal(17809.39m, cotizacion.Total);
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

            // Enterprise: 1,705.8825 x 0.83 = 1,415.882475 -> 1,415.88 por unidad
            Assert.Equal(1415.88m, cotizacion.PrecioUnitario);
            Assert.Equal(141588.00m, cotizacion.Subtotal);

            // 100 unidades: cae en el tramo mas alto vigente (15% desde 15 uds)
            Assert.Equal(0.15m, cotizacion.DescuentoPorcentaje);
            Assert.Equal(21238.20m, cotizacion.DescuentoMonto);

            // Servicios: soporte premium 49 + API 99 = 148
            Assert.Equal(2, cotizacion.Servicios.Count);
            Assert.Equal(148m, cotizacion.TotalServicios);

            // Base = 141,588.00 - 21,238.20 + 148.00 = 120,497.80
            Assert.Equal(19279.65m, cotizacion.Impuestos);
            Assert.Equal(139777.45m, cotizacion.Total);
        }

        // -----------------------------------------------------------------
        // Reglas comerciales vigentes: un unico tramo de 10% desde 5 unidades
        // y otro de 15% desde 15, tope de 100 unidades y una sola cotizacion
        // por correo de contacto.
        // -----------------------------------------------------------------

        [Theory]
        [InlineData(1, 0.00)]
        [InlineData(4, 0.00)]
        [InlineData(5, 0.10)]
        [InlineData(14, 0.10)]
        [InlineData(15, 0.15)]
        [InlineData(100, 0.15)]
        public async Task CalcularCotizacion_AplicaElTramoDeDescuentoDeCadaVolumen(int cantidad, decimal esperado)
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            var request = RequestBase();
            request.Cantidad = cantidad;

            var actionResult = await controller.CalcularCotizacion(request);

            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var cotizacion = Assert.IsType<CotizacionResponse>(okResult.Value);
            Assert.Equal(esperado, cotizacion.DescuentoPorcentaje);
        }

        [Fact]
        public async Task CalcularCotizacion_SegundaSolicitudDelMismoCorreo_DevuelveConflicto()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            var primera = await controller.CalcularCotizacion(RequestBase());
            Assert.IsType<OkObjectResult>(primera);

            var segunda = await controller.CalcularCotizacion(RequestBase());

            Assert.IsType<ConflictObjectResult>(segunda);
            Assert.Single(context.Cotizaciones);
        }

        [Fact]
        public async Task CalcularCotizacion_OtroCorreo_SiSeRegistra()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            await controller.CalcularCotizacion(RequestBase());

            var otra = RequestBase();
            otra.Email = "otra.empresa@prueba.com";
            var resultado = await controller.CalcularCotizacion(otra);

            Assert.IsType<OkObjectResult>(resultado);
            Assert.Equal(2, context.Cotizaciones.Count());
        }

        [Fact]
        public async Task CalcularCotizacion_CorreoConEspaciosYaCotizado_TambienSeBloquea()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            await controller.CalcularCotizacion(RequestBase());

            var conEspacios = RequestBase();
            conEspacios.Email = "  cliente@prueba.com  ";
            var resultado = await controller.CalcularCotizacion(conEspacios);

            Assert.IsType<ConflictObjectResult>(resultado);
        }

        [Fact]
        public async Task CalcularCotizacion_MismoCorreoEnMayusculas_TambienSeBloquea()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            await controller.CalcularCotizacion(RequestBase());

            var enMayusculas = RequestBase();
            enMayusculas.Email = "CLIENTE@PRUEBA.COM";
            var resultado = await controller.CalcularCotizacion(enMayusculas);

            Assert.IsType<ConflictObjectResult>(resultado);
            Assert.Single(context.Cotizaciones);
        }

        [Fact]
        public void CotizacionRequest_CantidadPorEncimaDelTope_NoEsValida()
        {
            var request = RequestBase();
            request.Cantidad = 101;

            var contexto = new System.ComponentModel.DataAnnotations.ValidationContext(request);
            var errores = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            bool valido = System.ComponentModel.DataAnnotations.Validator
                .TryValidateObject(request, contexto, errores, validateAllProperties: true);

            Assert.False(valido);
            Assert.Contains(errores, e => e.ErrorMessage!.Contains("entre 1 y 100"));
        }

        [Fact]
        public void CotizacionRequest_CantidadEnElTope_EsValida()
        {
            var request = RequestBase();
            request.Cantidad = 100;

            var contexto = new System.ComponentModel.DataAnnotations.ValidationContext(request);
            var errores = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            bool valido = System.ComponentModel.DataAnnotations.Validator
                .TryValidateObject(request, contexto, errores, validateAllProperties: true);

            Assert.True(valido);
        }

        // -----------------------------------------------------------------
        // Telefono: diez digitos exactos, sin prefijo ni separadores.
        // -----------------------------------------------------------------

        private static bool EsValido(CotizacionRequest request, out List<System.ComponentModel.DataAnnotations.ValidationResult> errores)
        {
            var contexto = new System.ComponentModel.DataAnnotations.ValidationContext(request);
            errores = new List<System.ComponentModel.DataAnnotations.ValidationResult>();
            return System.ComponentModel.DataAnnotations.Validator
                .TryValidateObject(request, contexto, errores, validateAllProperties: true);
        }

        [Theory]
        [InlineData("3312345678")]   // diez digitos
        [InlineData("4765783920")]
        public void CotizacionRequest_TelefonoDeDiezDigitos_EsValido(string telefono)
        {
            var request = RequestBase();
            request.Telefono = telefono;

            Assert.True(EsValido(request, out _));
        }

        [Theory]
        [InlineData("33123456789")]        // once digitos
        [InlineData("331234567")]          // nueve digitos
        [InlineData("+52 33 1234 5678")]   // con prefijo y espacios
        [InlineData("33-1234-5678")]       // con separadores
        [InlineData("")]                   // vacio
        public void CotizacionRequest_TelefonoFueraDeFormato_NoEsValido(string telefono)
        {
            var request = RequestBase();
            request.Telefono = telefono;

            Assert.False(EsValido(request, out var errores));
            Assert.NotEmpty(errores);
        }

        // -----------------------------------------------------------------
        // "Personalizacion de auras" se retiro del catalogo comercial.
        // -----------------------------------------------------------------

        [Fact]
        public void ReglasComerciales_YaNoOfreceLaPersonalizacionDeAuras()
        {
            Assert.DoesNotContain(
                CORSYNC.Core.Interfaces.ReglasComerciales.Servicios.Keys,
                clave => clave == "personalizacion");
        }

        [Fact]
        public async Task CalcularCotizacion_ServicioDePersonalizacion_YaNoSeCobra()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            var request = RequestBase();
            request.Servicios = new List<string> { "personalizacion" };

            var okResult = Assert.IsType<OkObjectResult>(await controller.CalcularCotizacion(request));
            var cotizacion = Assert.IsType<CotizacionResponse>(okResult.Value);

            Assert.Empty(cotizacion.Servicios);
            Assert.Equal(0m, cotizacion.TotalServicios);
        }

        // -----------------------------------------------------------------
        // Cambio de estado: llega como objeto JSON, no como cadena suelta.
        // -----------------------------------------------------------------

        [Fact]
        public async Task ActualizarEstado_ConEstadoValido_LoGuarda()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            var okAlta = Assert.IsType<OkObjectResult>(await controller.CalcularCotizacion(RequestBase()));
            var creada = Assert.IsType<CotizacionResponse>(okAlta.Value);

            var resultado = await controller.ActualizarEstado(
                creada.Id, new CambioEstadoRequest { Estado = "Contactado" });

            Assert.IsType<OkObjectResult>(resultado);
            Assert.Equal("Contactado", context.Cotizaciones.Single().Estado);
        }

        [Fact]
        public async Task ActualizarEstado_ConEstadoInvalido_DevuelveBadRequest()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            var okAlta = Assert.IsType<OkObjectResult>(await controller.CalcularCotizacion(RequestBase()));
            var creada = Assert.IsType<CotizacionResponse>(okAlta.Value);

            var resultado = await controller.ActualizarEstado(
                creada.Id, new CambioEstadoRequest { Estado = "Archivada" });

            Assert.IsType<BadRequestObjectResult>(resultado);
            Assert.Equal("Nueva", context.Cotizaciones.Single().Estado);
        }

        [Fact]
        public async Task ActualizarEstado_CotizacionInexistente_DevuelveNotFound()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            var resultado = await controller.ActualizarEstado(
                9999, new CambioEstadoRequest { Estado = "Cerrada" });

            Assert.IsType<NotFoundObjectResult>(resultado);
        }

        [Fact]
        public async Task GetParametros_PublicaLosMismosTramosQueSeAplican()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            var actionResult = await controller.GetParametros();
            var okResult = Assert.IsType<OkObjectResult>(actionResult);

            // Se compara contra ReglasComerciales, que es la fuente unica: si el
            // endpoint volviera a codificar los tramos a mano, esto lo detecta.
            var tipo = okResult.Value!.GetType();
            var tramos = tipo.GetProperty("DescuentosVolumen")!.GetValue(okResult.Value);
            var lista = ((System.Collections.IEnumerable)tramos!).Cast<object>().ToList();

            Assert.Equal(CORSYNC.Core.Interfaces.ReglasComerciales.TramosVolumen.Count, lista.Count);

            var maximo = tipo.GetProperty("CantidadMaxima")!.GetValue(okResult.Value);
            Assert.Equal(CORSYNC.Core.Interfaces.ReglasComerciales.CantidadMaxima, maximo);
        }

        [Fact]
        public async Task GetParametros_PublicaElPrecioRealDeCadaLicencia()
        {
            using var context = GetDbContext();
            var controller = GetController(context);

            var okResult = Assert.IsType<OkObjectResult>(await controller.GetParametros());
            var licencias = okResult.Value!.GetType().GetProperty("Licencias")!.GetValue(okResult.Value);
            var lista = ((System.Collections.IEnumerable)licencias!).Cast<object>().ToList();

            Assert.Equal(3, lista.Count);

            foreach (var item in lista)
            {
                var t = item.GetType();
                var clave = (string)t.GetProperty("Clave")!.GetValue(item)!;
                var precio = (decimal)t.GetProperty("Precio")!.GetValue(item)!;
                var factor = CORSYNC.Core.Interfaces.ReglasComerciales.FactorLicencia(clave);

                Assert.Equal(
                    System.Math.Round(PrecioLista * factor, 2, System.MidpointRounding.AwayFromZero),
                    precio);
            }
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
            Assert.Equal(1978.82m, guardada.CostoTotal);
        }
    }
}
