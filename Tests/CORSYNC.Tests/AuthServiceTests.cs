using System;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using CORSYNC.Core.Domain;
using CORSYNC.Infrastructure.Auth;

namespace CORSYNC.Tests
{
    public class AuthServiceTests
    {
        private readonly Mock<IConfiguration> _configMock;
        private readonly AuthService _service;

        public AuthServiceTests()
        {
            _configMock = new Mock<IConfiguration>();
            _configMock.Setup(c => c["TokenConfiguration:SecretKey"]).Returns("LLAVE_SECRETA_SUPER_LARGA_Y_COMPLEJA_PARA_JWT_2026");
            _configMock.Setup(c => c["TokenConfiguration:Issuer"]).Returns("CORSYNCServer");
            _configMock.Setup(c => c["TokenConfiguration:Audience"]).Returns("CORSYNCClients");

            _service = new AuthService(_configMock.Object);
        }

        [Fact]
        public void HashPassword_ReturnsNonNullAndNonEmptyHash()
        {
            // Act
            string hash = _service.HashPassword("mySecretPassword");

            // Assert
            Assert.NotNull(hash);
            Assert.NotEmpty(hash);
            Assert.NotEqual("mySecretPassword", hash);
        }

        [Fact]
        public void HashPassword_SameInput_ProducesDifferentHashes()
        {
            // Act
            string hash1 = _service.HashPassword("mySecretPassword");
            string hash2 = _service.HashPassword("mySecretPassword");

            // Assert
            Assert.NotEqual(hash1, hash2); // Salt is random per hash
        }

        [Fact]
        public void VerifyPassword_CorrectPassword_ReturnsTrue()
        {
            // Arrange
            string hash = _service.HashPassword("mySecretPassword");

            // Act
            bool result = _service.VerifyPassword("mySecretPassword", hash);

            // Assert
            Assert.True(result);
        }

        [Fact]
        public void VerifyPassword_WrongPassword_ReturnsFalse()
        {
            // Arrange
            string hash = _service.HashPassword("mySecretPassword");

            // Act
            bool result = _service.VerifyPassword("wrongPassword", hash);

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void VerifyPassword_InvalidHashFormat_ReturnsFalse()
        {
            // Act
            bool result = _service.VerifyPassword("mySecretPassword", "not_a_bcrypt_hash");

            // Assert
            Assert.False(result);
        }

        [Fact]
        public void GenerateJwtToken_ReturnsValidTokenWithExpectedClaims()
        {
            // Arrange
            var user = new Usuario
            {
                Id = 42,
                Username = "testuser",
                Email = "test@corsync.com",
                Role = "Cliente"
            };

            // Act
            string tokenString = _service.GenerateJwtToken(user);

            // Assert
            Assert.NotNull(tokenString);
            Assert.NotEmpty(tokenString);

            var handler = new JwtSecurityTokenHandler();
            Assert.True(handler.CanReadToken(tokenString));

            var jwtToken = handler.ReadJwtToken(tokenString);
            Assert.Equal("CORSYNCServer", jwtToken.Issuer);
            
            // Comprobar Claims
            Assert.Contains(jwtToken.Claims, c => c.Type == ClaimTypes.NameIdentifier && c.Value == "42");
            Assert.Contains(jwtToken.Claims, c => c.Type == ClaimTypes.Name && c.Value == "testuser");
            Assert.Contains(jwtToken.Claims, c => c.Type == ClaimTypes.Email && c.Value == "test@corsync.com");
            Assert.Contains(jwtToken.Claims, c => c.Type == ClaimTypes.Role && c.Value == "Cliente");
        }

        [Fact]
        public void GenerateRefreshToken_ReturnsValidToken()
        {
            // Act
            var refreshToken = _service.GenerateRefreshToken(42);

            // Assert
            Assert.NotNull(refreshToken);
            Assert.Equal(42, refreshToken.UsuarioId);
            Assert.NotEmpty(refreshToken.Token);
            Assert.False(refreshToken.Revocado);
            Assert.True(refreshToken.FechaExpiracion > DateTime.UtcNow);
        }

        [Fact]
        public void GetPrincipalFromExpiredToken_ValidSignature_ReturnsPrincipal()
        {
            // Arrange
            var user = new Usuario { Id = 42, Username = "test", Email = "a@b.com", Role = "Cliente" };
            var token = _service.GenerateJwtToken(user);

            // Act
            var principal = _service.GetPrincipalFromExpiredToken(token);

            // Assert
            Assert.NotNull(principal);
            var idClaim = principal.FindFirst(ClaimTypes.NameIdentifier);
            Assert.NotNull(idClaim);
            Assert.Equal("42", idClaim.Value);
        }

        [Fact]
        public void GetPrincipalFromExpiredToken_InvalidSignature_ReturnsNull()
        {
            // Act
            var principal = _service.GetPrincipalFromExpiredToken("invalid.jwt.token");

            // Assert
            Assert.Null(principal);
        }
    }
}
