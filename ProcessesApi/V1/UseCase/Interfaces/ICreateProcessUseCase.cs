using ProcessesApi.V1.Domain;
using System.Threading.Tasks;
using Hackney.Core.JWT;
using ProcessesApi.V1.Boundary.Request;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface ICreateProcessUseCase
    {
        Task<Process> Execute(CreateProcess request, ProcessName processName, Token token);
    }
}
