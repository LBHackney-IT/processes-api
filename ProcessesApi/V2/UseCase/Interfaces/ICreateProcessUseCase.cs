using ProcessesApi.V2.Domain;
using System.Threading.Tasks;
using Hackney.Core.JWT;
using ProcessesApi.V2.Boundary.Request;

namespace ProcessesApi.V2.UseCase.Interfaces
{
    public partial interface ICreateProcessUseCase
    {
        Task<Process> Execute(CreateProcess request, ProcessName processName, Token token);
    }
}
