using System;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Moq;
using Xunit;
using CORSYNC.Api.Controllers;
using CORSYNC.Core.Domain;
using CORSYNC.Core.DTOs;
using CORSYNC.Core.Interfaces;
using CORSYNC.Infrastructure.Auth;
using CORSYNC.Infrastructure.Database;

namespace CORSYNC.Tests
{
    public class AuthControllerTests
    {
        private (AdminDbContext Context, IAuthService AuthService) GetDeps()
        {
            var options = new DbContextOptionsBuilder<AdminDbContext>()
                .UseInMemoryDatabase(databaseName: $"CORSYNC_Admin_Test_{System.Guid.NewGuid()}")
                .Options;

            var context = new AdminDbContext(options);

            var configMock = new Mock<IConfiguration>();
            configMock.Setup(c => c["TokenConfiguration:SecretKey"]).Returns("LLAVE_SECRETA_SUPER_LARGA_Y_COMPLEJA_PARA_JWT_2026");
            configMock.Setup(c => c["TokenConfiguration:Issuer"]).Returns("CORSYNCServer");
            configMock.Setup(c => c["TokenConfiguration:Audience"]).Returns("CORSYNCClients");

            var authService = new AuthService(configMock.Object);

            return (context, authService);
        }

        [Fact]
        public async Task Register_ValidRequest_ReturnsCreatedWithTokenAndUserInfo()
        {
            // Arrange
            var (context, authService) = GetDeps();
            var controller = new AuthController(context, authService);
            var request = new RegisterRequest
            {
                Username = "nuevouser",
                Email = "nuevo@corsync.com",
                Password = "SuperPassword123!",
                NombreCompleto = "Nuevo Usuario"
            };

            // Act
            var actionResult = await controller.Register(request);

            // Assert
            var createdResult = Assert.IsType<CreatedAtActionResult>(actionResult);
            var authResponse = Assert.IsType<AuthResponse>(createdResult.Value);
            
            Assert.NotNull(authResponse.Token);
            Assert.Equal("nuevouser", authResponse.User.Username);
            Assert.Equal("nuevo@corsync.com", authResponse.User.Email);
            Assert.Equal("Nuevo Usuario", authResponse.User.NombreCompleto);
            Assert.Equal("Cliente", authResponse.User.Role); // Debe forzar el rol Cliente
            Assert.True(authResponse.User.Id > 0);

            // Verificar DB
            var userInDb = await context.Usuarios.FirstOrDefaultAsync(u => u.Username == "nuevouser");
            Assert.NotNull(userInDb);
            Assert.True(authService.VerifyPassword("SuperPassword123!", userInDb.PasswordHash));
        }

        [Fact]
        public async Task Register_DuplicateUsername_ReturnsConflict()
        {
            // Arrange
            var (context, authService) = GetDeps();
            context.Usuarios.Add(new Usuario
            {
                Username = "duplicado",
                Email = "other@corsync.com",
                PasswordHash = "hashed",
                Role = "Cliente"
            });
            await context.SaveChangesAsync();

            var controller = new AuthController(context, authService);
            var request = new RegisterRequest
            {
                Username = "duplicado",
                Email = "nuevo@corsync.com",
                Password = "SuperPassword123!"
            };

            // Act
            var actionResult = await controller.Register(request);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(actionResult);
            Assert.Equal("El nombre de usuario ya está en uso.", conflictResult.Value);
        }

        [Fact]
        public async Task Register_DuplicateEmail_ReturnsConflict()
        {
            // Arrange
            var (context, authService) = GetDeps();
            context.Usuarios.Add(new Usuario
            {
                Username = "other",
                Email = "duplicado@corsync.com",
                PasswordHash = "hashed",
                Role = "Cliente"
            });
            await context.SaveChangesAsync();

            var controller = new AuthController(context, authService);
            var request = new RegisterRequest
            {
                Username = "nuevo",
                Email = "duplicado@corsync.com",
                Password = "SuperPassword123!"
            };

            // Act
            var actionResult = await controller.Register(request);

            // Assert
            var conflictResult = Assert.IsType<ConflictObjectResult>(actionResult);
            Assert.Equal("El correo electrónico ya está en uso.", conflictResult.Value);
        }

        [Fact]
        public async Task Login_ValidCredentials_ReturnsOkWithTokenAndUserInfo()
        {
            // Arrange
            var (context, authService) = GetDeps();
            string hashedPassword = authService.HashPassword("miPassword123");
            context.Usuarios.Add(new Usuario
            {
                Username = "juan",
                Email = "juan@corsync.com",
                PasswordHash = hashedPassword,
                NombreCompleto = "Juan Perez",
                Role = "Cliente",
                Activo = true
            });
            await context.SaveChangesAsync();

            var controller = new AuthController(context, authService);
            var request = new LoginRequest
            {
                Username = "juan",
                Password = "miPassword123"
            };

            // Act
            var actionResult = await controller.Login(request);

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var authResponse = Assert.IsType<AuthResponse>(okResult.Value);
            
            Assert.NotNull(authResponse.Token);
            Assert.Equal("juan", authResponse.User.Username);
            Assert.Equal("Cliente", authResponse.User.Role);
        }

        [Fact]
        public async Task Login_WrongPassword_ReturnsUnauthorized()
        {
            // Arrange
            var (context, authService) = GetDeps();
            string hashedPassword = authService.HashPassword("miPassword123");
            context.Usuarios.Add(new Usuario
            {
                Username = "juan",
                Email = "juan@corsync.com",
                PasswordHash = hashedPassword,
                Role = "Cliente",
                Activo = true
            });
            await context.SaveChangesAsync();

            var controller = new AuthController(context, authService);
            var request = new LoginRequest
            {
                Username = "juan",
                Password = "passwordIncorrecto"
            };

            // Act
            var actionResult = await controller.Login(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult);
            Assert.Equal("Usuario o contraseña incorrectos.", unauthorizedResult.Value);
        }

        [Fact]
        public async Task Login_NonExistentUser_ReturnsUnauthorized()
        {
            // Arrange
            var (context, authService) = GetDeps();
            var controller = new AuthController(context, authService);
            var request = new LoginRequest
            {
                Username = "inexistente",
                Password = "anyPassword"
            };

            // Act
            var actionResult = await controller.Login(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult);
            Assert.Equal("Usuario o contraseña incorrectos.", unauthorizedResult.Value);
        }

        [Fact]
        public async Task Login_InactiveUser_ReturnsUnauthorized()
        {
            // Arrange
            var (context, authService) = GetDeps();
            string hashedPassword = authService.HashPassword("miPassword123");
            context.Usuarios.Add(new Usuario
            {
                Username = "inactivo",
                Email = "inactivo@corsync.com",
                PasswordHash = hashedPassword,
                Role = "Cliente",
                Activo = false // Cuenta desactivada
            });
            await context.SaveChangesAsync();

            var controller = new AuthController(context, authService);
            var request = new LoginRequest
            {
                Username = "inactivo",
                Password = "miPassword123"
            };

            // Act
            var actionResult = await controller.Login(request);

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult);
            Assert.Equal("Usuario o contraseña incorrectos.", unauthorizedResult.Value);
        }

        [Fact]
        public async Task Profile_AuthenticatedUser_ReturnsUserInfo()
        {
            // Arrange
            var (context, authService) = GetDeps();
            var user = new Usuario
            {
                Username = "perfiluser",
                Email = "perfil@corsync.com",
                PasswordHash = "hashed",
                NombreCompleto = "Usuario Perfil",
                Role = "Cliente",
                Activo = true
            };
            context.Usuarios.Add(user);
            await context.SaveChangesAsync();

            var controller = new AuthController(context, authService);

            // Simular autenticación (ClaimsPrincipal en HttpContext)
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()) };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            var claimsPrincipal = new ClaimsPrincipal(identity);
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = claimsPrincipal }
            };

            // Act
            var actionResult = await controller.GetProfile();

            // Assert
            var okResult = Assert.IsType<OkObjectResult>(actionResult);
            var userInfo = Assert.IsType<UserInfo>(okResult.Value);
            
            Assert.Equal(user.Id, userInfo.Id);
            Assert.Equal("perfiluser", userInfo.Username);
            Assert.Equal("perfil@corsync.com", userInfo.Email);
            Assert.Equal("Usuario Perfil", userInfo.NombreCompleto);
        }

        [Fact]
        public async Task Profile_NoNameIdentifierClaim_ReturnsUnauthorized()
        {
            // Arrange
            var (context, authService) = GetDeps();
            var controller = new AuthController(context, authService);

            // Simular HttpContext sin Claims
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(new ClaimsIdentity()) }
            };

            // Act
            var actionResult = await controller.GetProfile();

            // Assert
            var unauthorizedResult = Assert.IsType<UnauthorizedObjectResult>(actionResult);
            Assert.Contains("Token no contiene el identificador de usuario.", unauthorizedResult.Value?.ToString() ?? "");
        }

        [Fact]
        public async Task Profile_InvalidUserIdClaim_ReturnsBadRequest()
        {
            // Arrange
            var (context, authService) = GetDeps();
            var controller = new AuthController(context, authService);

            // Simular ClaimTypes.NameIdentifier no numérico
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "not-an-int") };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            // Act
            var actionResult = await controller.GetProfile();

            // Assert
            var badRequestResult = Assert.IsType<BadRequestObjectResult>(actionResult);
            Assert.Contains("Identificador de usuario inválido en el token.", badRequestResult.Value?.ToString() ?? "");
        }

        [Fact]
        public async Task Profile_UserNotFound_ReturnsNotFound()
        {
            // Arrange
            var (context, authService) = GetDeps();
            var controller = new AuthController(context, authService);

            // Simular ID de un usuario que no existe
            var claims = new[] { new Claim(ClaimTypes.NameIdentifier, "999") };
            var identity = new ClaimsIdentity(claims, "TestAuthType");
            controller.ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
            };

            // Act
            var actionResult = await controller.GetProfile();

            // Assert
            var notFoundResult = Assert.IsType<NotFoundObjectResult>(actionResult);
            Assert.Contains("Usuario no encontrado o inactivo.", notFoundResult.Value?.ToString() ?? "");
        }
    }
}
