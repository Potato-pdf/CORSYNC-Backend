using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.DTOs
{
    // ---------------------------------------------------------------------
    // Cotizacion
    // ---------------------------------------------------------------------

    public class CotizacionRequest
    {
        [Required(ErrorMessage = "El nombre es requerido.")]
        [MaxLength(120)]
        public string NombreCliente { get; set; } = string.Empty;

        [MaxLength(150)]
        public string Empresa { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es requerido.")]
        [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido.")]
        [MaxLength(120)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "El telefono es requerido.")]
        [MaxLength(40)]
        public string Telefono { get; set; } = string.Empty;

        [MaxLength(80)]
        public string Pais { get; set; } = string.Empty;

        [Range(1, 100000, ErrorMessage = "La cantidad debe estar entre 1 y 100000 unidades.")]
        public int Cantidad { get; set; } = 1;

        /// <summary>"Individual", "Corporativa" o "Enterprise".</summary>
        [MaxLength(30)]
        public string TipoLicencia { get; set; } = "Individual";

        /// <summary>Claves de servicios: soporte-premium, capacitacion, api-access, personalizacion.</summary>
        public List<string> Servicios { get; set; } = new List<string>();

        [MaxLength(2000)]
        public string? Mensaje { get; set; }

        public bool AceptaPrivacidad { get; set; }
    }

    public class ConceptoCosto
    {
        public string Concepto { get; set; } = string.Empty;
        public string Detalle { get; set; } = string.Empty;
        public decimal Importe { get; set; }
    }

    public class CotizacionResponse
    {
        public int Id { get; set; }
        public string Folio { get; set; } = string.Empty;
        public string NombreProducto { get; set; } = string.Empty;
        public int Cantidad { get; set; }
        public string TipoLicencia { get; set; } = string.Empty;

        /// <summary>Explosion de materiales valuada al costo promedio ponderado.</summary>
        public List<ConceptoCosto> DesgloseMateriaPrima { get; set; } = new List<ConceptoCosto>();

        public decimal CostoMateriaPrima { get; set; }
        public decimal CostoManoObra { get; set; }
        public decimal CostoIndirecto { get; set; }
        public decimal CostoUnitario { get; set; }
        public decimal MargenUtilidad { get; set; }
        public decimal PrecioLista { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }
        public decimal DescuentoPorcentaje { get; set; }
        public decimal DescuentoMonto { get; set; }
        public List<ConceptoCosto> Servicios { get; set; } = new List<ConceptoCosto>();
        public decimal TotalServicios { get; set; }
        public decimal Impuestos { get; set; }
        public decimal Total { get; set; }
        public DateTime FechaCotizacion { get; set; }
        public DateTime FechaVigencia { get; set; }
    }

    // ---------------------------------------------------------------------
    // Producto y costeo
    // ---------------------------------------------------------------------

    public class ProductoRequest
    {
        [Required]
        [MaxLength(120)]
        public string Nombre { get; set; } = string.Empty;

        [MaxLength(300)]
        public string Descripcion { get; set; } = string.Empty;

        [MaxLength(4000)]
        public string DescripcionLarga { get; set; } = string.Empty;

        [Range(0, 1000000)]
        public decimal ManoObraUnitaria { get; set; }

        [Range(0, 5)]
        public decimal OverheadPorcentaje { get; set; } = 0.25m;

        [Range(0, 5)]
        public decimal MargenUtilidad { get; set; } = 0.40m;

        public bool Activo { get; set; } = true;
    }

    public class RecetaRequest
    {
        [Range(1, int.MaxValue)]
        public int ProductoId { get; set; }

        [Range(1, int.MaxValue)]
        public int MateriaPrimaId { get; set; }

        [Range(0.0001, 1000000)]
        public decimal CantidadRequerida { get; set; }

        [Range(0, 1)]
        public decimal MermaPorcentaje { get; set; }
    }

    public class RenglonCostoResponse
    {
        public int RecetaId { get; set; }
        public int MateriaPrimaId { get; set; }
        public string MateriaPrima { get; set; } = string.Empty;
        public string UnidadMedida { get; set; } = string.Empty;
        public decimal CantidadRequerida { get; set; }
        public decimal MermaPorcentaje { get; set; }
        public decimal CantidadConMerma { get; set; }
        public decimal CostoUnitario { get; set; }
        public decimal CostoTotal { get; set; }
        public decimal Stock { get; set; }
        /// <summary>Unidades fabricables con el inventario disponible de este insumo.</summary>
        public int UnidadesPosibles { get; set; }
    }

    public class CostoProductoResponse
    {
        public int ProductoId { get; set; }
        public string Producto { get; set; } = string.Empty;
        public string MetodoCosteo { get; set; } = "Costo promedio ponderado";
        public List<RenglonCostoResponse> Materiales { get; set; } = new List<RenglonCostoResponse>();
        public decimal CostoMateriaPrima { get; set; }
        public decimal CostoManoObra { get; set; }
        public decimal CostoPrimo { get; set; }
        public decimal OverheadPorcentaje { get; set; }
        public decimal CostoIndirecto { get; set; }
        public decimal CostoUnitario { get; set; }
        public decimal MargenUtilidad { get; set; }
        public decimal PrecioLista { get; set; }
        /// <summary>Unidades fabricables con el inventario actual (minimo de la explosion).</summary>
        public int UnidadesFabricables { get; set; }
    }

    // ---------------------------------------------------------------------
    // Compras a proveedores
    // ---------------------------------------------------------------------

    public class CompraProveedorRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "Selecciona un proveedor válido.")]
        public int ProveedorId { get; set; }

        [MaxLength(500)]
        public string? Notas { get; set; }

        [MinLength(1, ErrorMessage = "La compra debe incluir al menos un insumo.")]
        public List<DetalleCompraRequest> Detalles { get; set; } = new List<DetalleCompraRequest>();
    }

    public class DetalleCompraRequest
    {
        [Range(1, int.MaxValue)]
        public int MateriaPrimaId { get; set; }

        [Range(0.0001, 1000000, ErrorMessage = "La cantidad debe ser mayor a cero.")]
        public decimal Cantidad { get; set; }

        [Range(0, 1000000, ErrorMessage = "El costo unitario no puede ser negativo.")]
        public decimal CostoUnitario { get; set; }
    }

    /// <summary>Efecto de una recepcion sobre el costo promedio ponderado de un insumo.</summary>
    public class ImpactoCosteoResponse
    {
        public int MateriaPrimaId { get; set; }
        public string MateriaPrima { get; set; } = string.Empty;
        public decimal StockAnterior { get; set; }
        public decimal CostoAnterior { get; set; }
        public decimal CantidadRecibida { get; set; }
        public decimal CostoCompra { get; set; }
        public decimal StockNuevo { get; set; }
        public decimal CostoPromedioNuevo { get; set; }
    }

    // ---------------------------------------------------------------------
    // Administracion de usuarios
    // ---------------------------------------------------------------------

    public class CrearUsuarioRequest
    {
        [Required(ErrorMessage = "El nombre de usuario es requerido.")]
        [MinLength(3)]
        [MaxLength(50)]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es requerido.")]
        [EmailAddress]
        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(100)]
        public string NombreCompleto { get; set; } = string.Empty;

        /// <summary>"Admin" o "Cliente".</summary>
        [Required]
        [MaxLength(20)]
        public string Role { get; set; } = "Cliente";

        /// <summary>Si se omite, el sistema genera una contrasena temporal.</summary>
        [MaxLength(100)]
        public string? Password { get; set; }

        public bool Activo { get; set; } = true;
    }

    public class ActualizarUsuarioRequest
    {
        [EmailAddress]
        [MaxLength(100)]
        public string? Email { get; set; }

        [MaxLength(100)]
        public string? NombreCompleto { get; set; }

        [MaxLength(20)]
        public string? Role { get; set; }

        public bool? Activo { get; set; }
    }

    public class CambiarPasswordRequest
    {
        [Required(ErrorMessage = "La contrasena actual es requerida.")]
        public string PasswordActual { get; set; } = string.Empty;

        [Required(ErrorMessage = "La nueva contrasena es requerida.")]
        [MinLength(8, ErrorMessage = "La nueva contrasena debe tener al menos 8 caracteres.")]
        [MaxLength(100)]
        public string PasswordNueva { get; set; } = string.Empty;
    }

    public class UsuarioCreadoResponse
    {
        public UserInfo Usuario { get; set; } = null!;
        /// <summary>Contrasena temporal generada. Solo se devuelve en el momento del alta.</summary>
        public string? PasswordTemporal { get; set; }
        public bool CorreoEnviado { get; set; }
        public string MensajeCorreo { get; set; } = string.Empty;
    }

    // ---------------------------------------------------------------------
    // Comentarios, contacto y compras de clientes
    // ---------------------------------------------------------------------

    public class ComentarioRequest
    {
        [Required(ErrorMessage = "El nombre es requerido.")]
        [MaxLength(120)]
        public string NombreUsuario { get; set; } = string.Empty;

        [EmailAddress]
        [MaxLength(120)]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "El comentario no puede estar vacio.")]
        [MaxLength(2000)]
        public string Contenido { get; set; } = string.Empty;

        [Range(1, 5, ErrorMessage = "La calificación debe estar entre 1 y 5 estrellas.")]
        public int Calificacion { get; set; } = 5;

        public int? ProductoId { get; set; }

        public int? CompraClienteId { get; set; }
    }

    public class ResponderComentarioRequest
    {
        [Required(ErrorMessage = "La respuesta no puede estar vacia.")]
        [MaxLength(2000)]
        public string Respuesta { get; set; } = string.Empty;
    }

    public class ResumenValoracionesResponse
    {
        public int Total { get; set; }
        public double Promedio { get; set; }
        public Dictionary<int, int> Distribucion { get; set; } = new Dictionary<int, int>();
    }

    public class ContactoRequest
    {
        [Required(ErrorMessage = "El nombre es requerido.")]
        [MaxLength(120)]
        public string Nombre { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es requerido.")]
        [EmailAddress]
        [MaxLength(120)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(40)]
        public string? Telefono { get; set; }

        [Required(ErrorMessage = "El asunto es requerido.")]
        [MaxLength(150)]
        public string Asunto { get; set; } = string.Empty;

        [Required(ErrorMessage = "El mensaje es requerido.")]
        [MaxLength(2000)]
        public string Mensaje { get; set; } = string.Empty;
    }

    public class CompraClienteRequest
    {
        [Range(1, int.MaxValue)]
        public int UsuarioId { get; set; }

        [Range(1, int.MaxValue)]
        public int ProductoId { get; set; }

        [Range(1, 10000)]
        public int Cantidad { get; set; } = 1;

        [Range(0, 10000000)]
        public decimal Monto { get; set; }

        [MaxLength(20)]
        public string Estado { get; set; } = "Procesando";

        [MaxLength(60)]
        public string? NumeroSerie { get; set; }
    }

    public class DashboardResponse
    {
        public int TotalClientes { get; set; }
        public int TotalAdministradores { get; set; }
        public int ComentariosPendientes { get; set; }
        public int ComentariosAprobados { get; set; }
        public double CalificacionPromedio { get; set; }
        public int CotizacionesTotales { get; set; }
        public int CotizacionesNuevas { get; set; }
        public decimal MontoCotizado { get; set; }
        public int MensajesSinAtender { get; set; }
        public int Proveedores { get; set; }
        public int InsumosBajoMinimo { get; set; }
        public decimal ValorInventario { get; set; }
        public int UnidadesFabricables { get; set; }
        public decimal CostoUnitarioProducto { get; set; }
        public decimal PrecioListaProducto { get; set; }
    }
}
