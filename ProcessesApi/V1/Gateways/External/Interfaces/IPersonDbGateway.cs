using System;
using System.Threading.Tasks;
using Hackney.Shared.Person;
using Hackney.Shared.Person.Boundary.Request;
using Hackney.Shared.Person.Infrastructure;
using Hackney.Shared.Processes.Infrastructure;

namespace ProcessesApi.V1.Gateways
{
    public interface IPersonDbGateway
    {
        Task<Person> GetPersonById(Guid id);
        Task<UpdateEntityResult<PersonDbEntity>> UpdatePersonByIdAsync(Guid id, UpdatePersonRequestObject updatePersonRequest);
    }
}
