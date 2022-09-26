using Hackney.Core.DynamoDb;
using Hackney.Shared.Processes.Boundary.Request;
using Hackney.Shared.Processes.Boundary.Response;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface IGetProcessesByTargetIdUseCase
    {
        Task<PagedResult<ProcessResponse>> Execute(GetProcessesByTargetIdRequest request);
    }
}
