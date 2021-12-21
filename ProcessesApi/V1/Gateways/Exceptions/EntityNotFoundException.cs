using System;

namespace ProcessesApi.V1.Gateways.Exceptions
{
    // TODO: Possibly move to common libarary (copied from Tenure Listener)
    public class EntityNotFoundException : Exception
    {
        public string EntityName { get; }
        public Guid Id { get; }

        public EntityNotFoundException(string entityName, Guid id)
            : base($"{entityName} with id {id} not found.")
        {
            EntityName = entityName;
            Id = id;
        }
    }
}