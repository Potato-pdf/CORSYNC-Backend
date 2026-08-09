using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using CORSYNC.Core.Interfaces;

namespace CORSYNC.Infrastructure.Media
{
    /// <inheritdoc cref="IAlmacenImagenes"/>
    public class AlmacenImagenesLocal : IAlmacenImagenes
    {
        /// <summary>5 MB por imagen: suficiente para una foto de producto sin llenar el disco.</summary>
        public const long TamanoMaximo = 5 * 1024 * 1024;

        private static readonly HashSet<string> ExtensionesPermitidas =
            new(StringComparer.OrdinalIgnoreCase) { ".jpg", ".jpeg", ".png", ".webp", ".gif" };

        private static readonly HashSet<string> TiposPermitidos =
            new(StringComparer.OrdinalIgnoreCase)
            { "image/jpeg", "image/png", "image/webp", "image/gif" };

        /// <summary>
        /// Firmas binarias de cada formato. Se comprueban porque la extensión y el
        /// content-type los controla quien sube el archivo, y ninguno de los dos
        /// garantiza que el contenido sea realmente una imagen.
        /// </summary>
        private static readonly (byte[] Firma, int Desplazamiento)[] Firmas =
        {
            (new byte[] { 0xFF, 0xD8, 0xFF }, 0),                                  // JPEG
            (new byte[] { 0x89, 0x50, 0x4E, 0x47 }, 0),                            // PNG
            (new byte[] { 0x47, 0x49, 0x46, 0x38 }, 0),                            // GIF
            (new byte[] { 0x52, 0x49, 0x46, 0x46 }, 0)                             // WEBP (RIFF)
        };

        private readonly string _raiz;
        private readonly ILogger<AlmacenImagenesLocal> _logger;

        public AlmacenImagenesLocal(string raizWebRoot, ILogger<AlmacenImagenesLocal> logger)
        {
            _raiz = raizWebRoot;
            _logger = logger;
        }

        public async Task<ResultadoImagen> GuardarAsync(
            Stream contenido, string nombreOriginal, string contentType, long tamano, int productoId)
        {
            if (tamano <= 0)
            {
                return new ResultadoImagen { Error = "El archivo está vacío." };
            }

            if (tamano > TamanoMaximo)
            {
                return new ResultadoImagen
                {
                    Error = $"La imagen pesa {tamano / 1024 / 1024} MB. El máximo permitido son 5 MB."
                };
            }

            var extension = Path.GetExtension(nombreOriginal);
            if (string.IsNullOrWhiteSpace(extension) || !ExtensionesPermitidas.Contains(extension))
            {
                return new ResultadoImagen
                {
                    Error = "Formato no permitido. Usa JPG, PNG, WEBP o GIF."
                };
            }

            if (!TiposPermitidos.Contains(contentType))
            {
                return new ResultadoImagen { Error = "El tipo de contenido del archivo no es una imagen válida." };
            }

            if (!await EsImagenRealAsync(contenido))
            {
                return new ResultadoImagen
                {
                    Error = "El contenido del archivo no corresponde a una imagen."
                };
            }

            try
            {
                var carpeta = Path.Combine(_raiz, "uploads", "productos", productoId.ToString());
                Directory.CreateDirectory(carpeta);

                // El nombre lo genera el servidor: así ningún nombre recibido puede
                // contener "../" ni sobrescribir un archivo existente.
                var nombreArchivo = $"{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
                var destino = Path.Combine(carpeta, nombreArchivo);

                contenido.Position = 0;
                await using (var salida = new FileStream(destino, FileMode.CreateNew, FileAccess.Write))
                {
                    await contenido.CopyToAsync(salida);
                }

                return new ResultadoImagen
                {
                    Exito = true,
                    Url = $"/uploads/productos/{productoId}/{nombreArchivo}",
                    NombreArchivo = nombreArchivo,
                    TamanoBytes = tamano
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "No se pudo guardar la imagen del producto {ProductoId}", productoId);
                return new ResultadoImagen { Error = "No se pudo guardar la imagen en el servidor." };
            }
        }

        public void Eliminar(int productoId, string nombreArchivo)
        {
            if (string.IsNullOrWhiteSpace(nombreArchivo))
            {
                return;
            }

            // Se descarta cualquier nombre con separadores: sólo puede borrarse un
            // archivo que esté directamente en la carpeta del producto.
            var soloNombre = Path.GetFileName(nombreArchivo);
            if (soloNombre != nombreArchivo)
            {
                _logger.LogWarning("Nombre de archivo sospechoso al eliminar: {Nombre}", nombreArchivo);
                return;
            }

            try
            {
                var ruta = Path.Combine(_raiz, "uploads", "productos", productoId.ToString(), soloNombre);
                if (File.Exists(ruta))
                {
                    File.Delete(ruta);
                }
            }
            catch (Exception ex)
            {
                // El registro ya se borró de la base; un archivo huérfano no debe
                // hacer fallar la operación.
                _logger.LogWarning(ex, "No se pudo borrar el archivo {Nombre}", soloNombre);
            }
        }

        private static async Task<bool> EsImagenRealAsync(Stream contenido)
        {
            if (!contenido.CanSeek)
            {
                return true;
            }

            contenido.Position = 0;
            var cabecera = new byte[12];
            int leidos = await contenido.ReadAsync(cabecera.AsMemory(0, cabecera.Length));
            contenido.Position = 0;

            if (leidos < 4)
            {
                return false;
            }

            return Firmas.Any(f =>
                leidos >= f.Desplazamiento + f.Firma.Length &&
                f.Firma.Select((b, i) => cabecera[f.Desplazamiento + i] == b).All(x => x));
        }
    }
}
