using System.Threading.Tasks;

namespace CORSYNC.Core.Interfaces
{
    public interface IGamificationService
    {
        Task VerificarMedallasAsync(int usuarioId);
        Task ActualizarProgresoDesafioAsync(int usuarioId, string tipoDesafio, int valorIncremento);
    }
}
