using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface IGetByIdUseCase
    {
        Task<Process> Execute(ProcessesQuery query);
    }
}
