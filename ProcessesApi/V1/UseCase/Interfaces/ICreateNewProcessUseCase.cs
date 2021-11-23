using System.Threading.Tasks;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Boundary.Response;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface ICreateNewProcessUsecase
    {
        Task<ProcessResponse> Execute(CreateProcessQuery createProcessQuery, string processName);
    }
}
