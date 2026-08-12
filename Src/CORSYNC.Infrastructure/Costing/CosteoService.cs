using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using CORSYNC.Core.Domain;
using CORSYNC.Core.DTOs;
using CORSYNC.Core.Interfaces;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Infrastructure.Costing
{
    /// <inheritdoc cref="ICosteoService"/>
    public class CosteoService : ICosteoService
    {
        private readonly AdminDbContext _context;

        public CosteoService(AdminDbContext context)
        {
            _context = context;
        }

        public async Task<CostoProductoResponse?> CalcularCostoProductoAsync(int productoId)
        {
            var producto = await _context.Productos.FirstOrDefaultAsync(p => p.Id == productoId);
            if (producto == null)
            {
                return null;
            }

            var receta = await _context.RecetasProductos
                .Include(r => r.MateriaPrima)
                .Where(r => r.ProductoId == productoId)
                .OrderBy(r => r.Id)
                .ToListAsync();

            var respuesta = new CostoProductoResponse
            {
                ProductoId = producto.Id,
                Producto = producto.Nombre,
                CostoManoObra = producto.ManoObraUnitaria,
                OverheadPorcentaje = producto.OverheadPorcentaje,
                MargenUtilidad = producto.MargenUtilidad
            };

            int unidadesFabricables = int.MaxValue;

            foreach (var renglon in receta)
            {
                var insumo = renglon.MateriaPrima;
                if (insumo == null)
                {
                    continue;
                }

                // La merma incrementa el consumo real de cada insumo por unidad producida.
                decimal cantidadConMerma = renglon.CantidadRequerida * (1 + renglon.MermaPorcentaje);
                decimal costoTotal = Math.Round(cantidadConMerma * insumo.CostoUnidad, 2, MidpointRounding.AwayFromZero);

                int posibles = cantidadConMerma > 0
                    ? (int)Math.Floor(insumo.Stock / cantidadConMerma)
                    : int.MaxValue;
                unidadesFabricables = Math.Min(unidadesFabricables, posibles);

                respuesta.Materiales.Add(new RenglonCostoResponse
                {
                    RecetaId = renglon.Id,
                    MateriaPrimaId = insumo.Id,
                    MateriaPrima = insumo.Nombre,
                    UnidadMedida = insumo.UnidadMedida,
                    CantidadRequerida = renglon.CantidadRequerida,
                    MermaPorcentaje = renglon.MermaPorcentaje,
                    CantidadConMerma = Math.Round(cantidadConMerma, 4, MidpointRounding.AwayFromZero),
                    CostoUnitario = insumo.CostoUnidad,
                    CostoTotal = costoTotal,
                    Stock = insumo.Stock,
                    UnidadesPosibles = posibles
                });
            }

            respuesta.CostoMateriaPrima = Math.Round(respuesta.Materiales.Sum(m => m.CostoTotal), 2, MidpointRounding.AwayFromZero);
            respuesta.CostoPrimo = Math.Round(respuesta.CostoMateriaPrima + respuesta.CostoManoObra, 2, MidpointRounding.AwayFromZero);
            respuesta.CostoIndirecto = Math.Round(respuesta.CostoPrimo * producto.OverheadPorcentaje, 2, MidpointRounding.AwayFromZero);
            respuesta.CostoUnitario = Math.Round(respuesta.CostoPrimo + respuesta.CostoIndirecto, 2, MidpointRounding.AwayFromZero);
            respuesta.PrecioLista = Math.Round(respuesta.CostoUnitario * (1 + producto.MargenUtilidad), 2, MidpointRounding.AwayFromZero);
            respuesta.UnidadesFabricables = receta.Count == 0 || unidadesFabricables == int.MaxValue ? 0 : Math.Max(unidadesFabricables, 0);

            return respuesta;
        }

        public async Task<ImpactoCosteoResponse?> RegistrarEntradaInventarioAsync(int materiaPrimaId, decimal cantidad, decimal costoCompra)
        {
            var insumo = await _context.MateriasPrimas.FindAsync(materiaPrimaId);
            if (insumo == null || cantidad <= 0)
            {
                return null;
            }

            decimal stockAnterior = insumo.Stock;
            decimal costoAnterior = insumo.CostoUnidad;
            decimal stockNuevo = stockAnterior + cantidad;

            // Costo promedio ponderado: el inventario existente y la entrada nueva se
            // promedian en proporcion a sus cantidades.
            decimal costoPromedio = stockNuevo > 0
                ? ((stockAnterior * costoAnterior) + (cantidad * costoCompra)) / stockNuevo
                : costoCompra;

            insumo.Stock = stockNuevo;
            insumo.CostoUnidad = Math.Round(costoPromedio, 4, MidpointRounding.AwayFromZero);

            return new ImpactoCosteoResponse
            {
                MateriaPrimaId = insumo.Id,
                MateriaPrima = insumo.Nombre,
                StockAnterior = stockAnterior,
                CostoAnterior = costoAnterior,
                CantidadRecibida = cantidad,
                CostoCompra = costoCompra,
                StockNuevo = insumo.Stock,
                CostoPromedioNuevo = insumo.CostoUnidad
            };
        }

        public async Task<SalidaCosteoResponse?> RegistrarSalidaInventarioAsync(int materiaPrimaId, decimal cantidad)
        {
            var insumo = await _context.MateriasPrimas.FindAsync(materiaPrimaId);
            if (insumo == null || cantidad <= 0 || cantidad > insumo.Stock)
            {
                return null;
            }

            return AplicarSalida(insumo, cantidad);
        }

        public async Task<ConsumoProduccionResponse?> RegistrarConsumoProduccionAsync(int productoId, int unidades)
        {
            var producto = await _context.Productos.FirstOrDefaultAsync(p => p.Id == productoId);
            if (producto == null || unidades <= 0)
            {
                return null;
            }

            var receta = await _context.RecetasProductos
                .Include(r => r.MateriaPrima)
                .Where(r => r.ProductoId == productoId)
                .OrderBy(r => r.Id)
                .ToListAsync();

            var respuesta = new ConsumoProduccionResponse
            {
                ProductoId = producto.Id,
                Producto = producto.Nombre,
                Unidades = unidades
            };

            if (receta.Count == 0)
            {
                respuesta.Faltantes.Add("El producto no tiene explosión de materiales.");
                return respuesta;
            }

            // Se calcula el consumo completo y se valida antes de tocar el inventario:
            // si falta un insumo no se descuenta ninguno, para no dejar existencias a
            // medio consumir por una produccion que no puede completarse.
            var plan = new List<(MateriaPrima Insumo, decimal Cantidad)>();
            foreach (var renglon in receta)
            {
                var insumo = renglon.MateriaPrima;
                if (insumo == null)
                {
                    continue;
                }

                decimal requerido = Math.Round(
                    renglon.CantidadRequerida * (1 + renglon.MermaPorcentaje) * unidades,
                    4, MidpointRounding.AwayFromZero);

                if (requerido > insumo.Stock)
                {
                    respuesta.Faltantes.Add(
                        $"{insumo.Nombre}: se requieren {requerido:0.####} {insumo.UnidadMedida} y hay {insumo.Stock:0.####}.");
                }

                plan.Add((insumo, requerido));
            }

            if (respuesta.Faltantes.Count > 0)
            {
                return respuesta;
            }

            foreach (var (insumo, requerido) in plan)
            {
                respuesta.Salidas.Add(AplicarSalida(insumo, requerido));
            }

            respuesta.CostoMateriaPrimaConsumida = Math.Round(
                respuesta.Salidas.Sum(s => s.ImporteSalida), 2, MidpointRounding.AwayFromZero);
            respuesta.Aplicado = true;

            return respuesta;
        }

        /// <summary>
        /// Descuenta la cantidad del insumo dejando intacto su costo promedio: la
        /// salida se valua al ultimo promedio calculado y el saldo restante conserva
        /// ese mismo costo por unidad.
        /// </summary>
        private static SalidaCosteoResponse AplicarSalida(MateriaPrima insumo, decimal cantidad)
        {
            decimal stockAnterior = insumo.Stock;
            decimal costoPromedio = insumo.CostoUnidad;

            insumo.Stock = stockAnterior - cantidad;

            return new SalidaCosteoResponse
            {
                MateriaPrimaId = insumo.Id,
                MateriaPrima = insumo.Nombre,
                UnidadMedida = insumo.UnidadMedida,
                StockAnterior = stockAnterior,
                CantidadSalida = cantidad,
                StockNuevo = insumo.Stock,
                CostoPromedio = costoPromedio,
                ImporteSalida = Math.Round(cantidad * costoPromedio, 2, MidpointRounding.AwayFromZero),
                SaldoValorizado = Math.Round(insumo.Stock * costoPromedio, 2, MidpointRounding.AwayFromZero)
            };
        }
    }
}
