using CORSYNC.Core.Domain;

namespace CORSYNC.Core.Interfaces
{
    public interface IAuthService
    {
        string HashPassword(string password);
        bool VerifyPassword(string password, string passwordHash);
        string GenerateJwtToken(Usuario user);
    }
}
