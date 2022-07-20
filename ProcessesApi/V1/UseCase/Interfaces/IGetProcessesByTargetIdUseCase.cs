using Hackney.Core.DynamoDb;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Boundary.Response;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface IGetProcessesByTargetIdUseCase
    {
        Task<PagedResult<ProcessResponse>> Execute(GetProcessesByTargetIdRequest request);
    }
}
