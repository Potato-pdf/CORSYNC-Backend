using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
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
    }
}
