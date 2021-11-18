using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.UseCase.Interfaces;
using Hackney.Core.Logging;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class CreateNewProcessUseCase : ICreateNewProcessUsecase
    {
        private IProcessesGateway _gateway;
        public CreateNewProcessUseCase(IProcessesGateway gateway)
        {
            _gateway = gateway;
        }
        [LogCall]
        public async Task<ProcessesResponse> Execute(CreateProcessQuery createProcessQuery, string processName)
        {
            var process = await _gateway.CreateNewProcess(createProcessQuery, processName).ConfigureAwait(false);
            return process.ToResponse();
        }
    }
}
