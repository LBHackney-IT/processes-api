using Hackney.Shared.Person;
using System;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Helpers
{
    public interface IGetPersonByIdHelper
    {
        Task<Person> GetPersonById(Guid incomingTenantId);
    }
}
