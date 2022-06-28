using System;
using System.Threading.Tasks;
using Hackney.Shared.Person.Boundary.Request;

namespace ProcessesApi.V1.Gateways
{
    public interface IPersonApiGateway
    {
        public Task UpdatePersonById(Guid id, UpdatePersonRequestObject updatePersonRequestObject, int? ifMatch);
    }
}
