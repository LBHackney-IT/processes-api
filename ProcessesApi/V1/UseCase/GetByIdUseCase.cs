using Hackney.Core.Logging;
using Hackney.Shared.Processes.Boundary.Request;
using Hackney.Shared.Processes.Domain;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase.Interfaces;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class GetProcessByIdUseCase : IGetByIdUseCase
    {
        private IProcessesGateway _gateway;
        public GetProcessByIdUseCase(IProcessesGateway gateway)
        {
            _gateway = gateway;
        }

        [LogCall]
        public async Task<Process> Execute(ProcessQuery query)
        {
            var entity = await _gateway.GetProcessById(query.Id).ConfigureAwait(false);
            return entity;
        }
    }
}
