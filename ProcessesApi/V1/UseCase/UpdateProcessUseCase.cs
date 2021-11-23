using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.UseCase.Interfaces;
using Hackney.Core.Logging;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class UpdateProcessUseCase : IUpdateProcessUsecase
    {
        private IProcessesGateway _gateway;
        public UpdateProcessUseCase(IProcessesGateway gateway)
        {
            _gateway = gateway;
        }
        [LogCall]
        public async Task<ProcessResponse> Execute(UpdateProcessQueryObject requestObject, UpdateProcessQuery query, int? ifMatch)
        {
            var process = await _gateway.UpdateProcess(requestObject, query, ifMatch).ConfigureAwait(false);
            return process.ToResponse();
        }
    }
}
