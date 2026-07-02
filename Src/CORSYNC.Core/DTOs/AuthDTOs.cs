using System;
using System.ComponentModel.DataAnnotations;

namespace CORSYNC.Core.DTOs
{
    public class RegisterRequest
    {
        [Required(ErrorMessage = "El nombre de usuario es requerido.")]
        [MinLength(3, ErrorMessage = "El nombre de usuario debe tener al menos 3 caracteres.")]
        [MaxLength(50, ErrorMessage = "El nombre de usuario no puede exceder los 50 caracteres.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "El correo electrónico es requerido.")]
        [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido.")]
        [MaxLength(100, ErrorMessage = "El correo electrónico no puede exceder los 100 caracteres.")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida.")]
        [MinLength(8, ErrorMessage = "La contraseña debe tener al menos 8 caracteres.")]
        [MaxLength(100, ErrorMessage = "La contraseña no puede exceder los 100 caracteres.")]
        public string Password { get; set; } = string.Empty;

        [MaxLength(100, ErrorMessage = "El nombre completo no puede exceder los 100 caracteres.")]
        public string NombreCompleto { get; set; } = string.Empty;
    }

    public class LoginRequest
    {
        [Required(ErrorMessage = "El nombre de usuario es requerido.")]
        public string Username { get; set; } = string.Empty;

        [Required(ErrorMessage = "La contraseña es requerida.")]
        public string Password { get; set; } = string.Empty;
    }

    public class AuthResponse
    {
        public string Token { get; set; } = string.Empty;
        public string RefreshToken { get; set; } = string.Empty;
        public DateTime Expiration { get; set; }
        public UserInfo User { get; set; } = null!;
    }

    public class RefreshTokenRequest
    {
        [Required(ErrorMessage = "El token de acceso es requerido.")]
        public string Token { get; set; } = string.Empty;

        [Required(ErrorMessage = "El token de refresco es requerido.")]
        public string RefreshToken { get; set; } = string.Empty;
    }

    public class UserInfo
    {
        public int Id { get; set; }
        public string Username { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string NombreCompleto { get; set; } = string.Empty;
        public string Role { get; set; } = string.Empty;
        public DateTime FechaRegistro { get; set; }
        public string NombreEspiritual { get; set; } = string.Empty;
        public string SignoZodiacal { get; set; } = string.Empty;
        public string? FotoUrl { get; set; }
    }
}
