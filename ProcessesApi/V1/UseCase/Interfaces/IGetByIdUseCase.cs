using Hackney.Shared.Processes.Boundary.Request;
using Hackney.Shared.Processes.Domain;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface IGetByIdUseCase
    {
        Task<Process> Execute(ProcessQuery query);
    }
}
