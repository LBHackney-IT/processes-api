using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase.Interfaces;
using Hackney.Core.Logging;
using System.Threading.Tasks;
using ProcessesApi.V1.Domain.SoleToJoint;

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
        public async Task<SoleToJointProcess> Execute(ProcessesQuery query)
        {
            var entity = await _gateway.GetProcessById(query.Id).ConfigureAwait(false);
            return entity;
        }
    }
}
