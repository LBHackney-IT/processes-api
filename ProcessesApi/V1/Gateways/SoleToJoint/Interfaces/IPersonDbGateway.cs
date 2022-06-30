using System;
using System.Threading.Tasks;
using Hackney.Shared.Person;

namespace ProcessesApi.V1.Gateways
{
    public interface IPersonDbGateway
    {
        public Task<Person> GetPersonById(Guid id);
        public Task<Person> UpdatePersonById(Person person);
    }
}
