using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase.Interfaces;
using Hackney.Core.Logging;
using System;
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
        public async Task<ProcessesResponse> Execute(Guid id)
        {
            var entity = await _gateway.GetProcessById(id).ConfigureAwait(false);
            return entity.ToResponse();
        }
    }
}
