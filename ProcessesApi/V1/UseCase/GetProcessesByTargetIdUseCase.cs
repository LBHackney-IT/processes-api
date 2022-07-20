using Hackney.Core.DynamoDb;
using Hackney.Core.Logging;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase.Interfaces;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class GetProcessesByTargetIdUseCase : IGetProcessesByTargetIdUseCase
    {
        private IProcessesGateway _gateway;

        public GetProcessesByTargetIdUseCase(IProcessesGateway gateway)
        {
            _gateway = gateway;
        }

        [LogCall]
        public async Task<PagedResult<ProcessResponse>> Execute(GetProcessesByTargetIdRequest request)
        {
            var response = await _gateway.GetProcessesByTargetId(request).ConfigureAwait(false);
            return new PagedResult<ProcessResponse>(response.Results.ToResponse(), response.PaginationDetails);
        }
    }
}
