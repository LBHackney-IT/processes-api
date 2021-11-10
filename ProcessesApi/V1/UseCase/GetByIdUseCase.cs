using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase.Interfaces;
using Hackney.Core.Logging;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    //TODO: Rename class name and interface name to reflect the entity they are representing eg. GetClaimantByIdUseCase
    public class GetByIdUseCase : IGetByIdUseCase
    {
        private IExampleDynamoGateway _gateway;
        public GetByIdUseCase(IExampleDynamoGateway gateway)
        {
            _gateway = gateway;
        }
        [LogCall]
        //TODO: rename id to the name of the identifier that will be used for this API, the type may also need to change
        public async Task<ResponseObject> Execute(int id)
        {
            var entity = await _gateway.GetEntityById(id).ConfigureAwait(false);
            return entity.ToResponse();
        }
    }
}
