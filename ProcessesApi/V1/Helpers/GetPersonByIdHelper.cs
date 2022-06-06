using Hackney.Shared.Person;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Gateways.Exceptions;
using System;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Helpers
{
    public class GetPersonByIdHelper : IGetPersonByIdHelper
    {
        private readonly IPersonDbGateway _personDbGateway;

        public GetPersonByIdHelper(IPersonDbGateway personDbGateway)
        {
            _personDbGateway = personDbGateway;
        }
        public async Task<Person> GetPersonById(Guid incomingTenantId)
        {
            var person = await _personDbGateway.GetPersonById(incomingTenantId).ConfigureAwait(false);
            if (person is null) throw new PersonNotFoundException(incomingTenantId);
            return person;
        }
    }
}
