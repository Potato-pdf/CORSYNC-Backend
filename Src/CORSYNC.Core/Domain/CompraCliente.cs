using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace CORSYNC.Core.Domain
{
    /// <summary>
    /// Compra realizada por un cliente. Sirve al panel de cliente para listar sus
    /// adquisiciones y habilitar la opinion sobre el producto adquirido.
    /// </summary>
    public class CompraCliente
    {
        public int Id { get; set; }

        [Required]
        public int UsuarioId { get; set; }
        public Usuario? Usuario { get; set; }

        [Required]
        public int ProductoId { get; set; }
        public Producto? Producto { get; set; }

        [MaxLength(50)]
        public string Folio { get; set; } = string.Empty;

        public int Cantidad { get; set; } = 1;

        [Column(TypeName = "decimal(18,2)")]
        public decimal Monto { get; set; }

        /// <summary>"Procesando", "Enviado", "Entregado" o "Cancelado".</summary>
        [Required]
        [MaxLength(20)]
        public string Estado { get; set; } = "Procesando";

        /// <summary>Numero de serie del dispositivo entregado al cliente.</summary>
        [MaxLength(60)]
        public string? NumeroSerie { get; set; }

        public bool Resenado { get; set; }

        public DateTime FechaCompra { get; set; } = DateTime.UtcNow;
    }
}
