using System;

namespace ProcessesApi.V1.Gateways.Exceptions
{
    public class PersonNotFoundException : EntityNotFoundException
    {
        public PersonNotFoundException(Guid id)
            : base("Person", id)
        { }
    }
}
