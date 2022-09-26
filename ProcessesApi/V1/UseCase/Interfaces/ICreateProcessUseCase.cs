using Hackney.Shared.Processes.Domain;
using System.Threading.Tasks;
using Hackney.Core.JWT;
using Hackney.Shared.Processes.Boundary.Request;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface ICreateProcessUseCase
    {
        Task<Process> Execute(CreateProcess request, ProcessName processName, Token token);
    }
}
