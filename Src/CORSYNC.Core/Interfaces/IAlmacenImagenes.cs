using System.IO;
using System.Threading.Tasks;

namespace CORSYNC.Core.Interfaces
{
    public class ResultadoImagen
    {
        public bool Exito { get; set; }
        public string Error { get; set; } = string.Empty;

        /// <summary>Ruta pública para el navegador, p. ej. /uploads/productos/1/abc.jpg</summary>
        public string Url { get; set; } = string.Empty;

        public string NombreArchivo { get; set; } = string.Empty;
        public long TamanoBytes { get; set; }
    }

    /// <summary>
    /// Guarda en disco las imágenes que sube el administrador. El nombre del archivo
    /// siempre lo genera el servidor: el nombre que envía el cliente no es de fiar y
    /// podría contener rutas relativas para escapar del directorio de destino.
    /// </summary>
    public interface IAlmacenImagenes
    {
        Task<ResultadoImagen> GuardarAsync(Stream contenido, string nombreOriginal, string contentType, long tamano, int productoId);

        /// <summary>Borra el archivo si existe. No falla si ya no está.</summary>
        void Eliminar(int productoId, string nombreArchivo);
    }
}
