using Hackney.Core.JWT;
using Hackney.Core.Sns;
using Hackney.Shared.Person;
using Hackney.Shared.Person.Infrastructure;
using Hackney.Shared.Processes.Infrastructure;
using ProcessesApi.V1.Infrastructure;
using System;

namespace ProcessesApi.V1.Factories
{
    public class PersonSnsFactory : IPersonSnsFactory
    {
        public EntityEventSns Create(Person person, Token token)
        {
            return new EntityEventSns
            {
                CorrelationId = Guid.NewGuid(),
                DateTime = DateTime.UtcNow,
                EntityId = person.Id,
                Id = Guid.NewGuid(),
                EventType = PersonEventConstants.PERSON_CREATED_EVENT,
                Version = PersonEventConstants.V1_VERSION,
                SourceDomain = PersonEventConstants.SOURCE_DOMAIN,
                SourceSystem = PersonEventConstants.SOURCE_SYSTEM,
                User = new User
                {
                    Name = token.Name,
                    Email = token.Email
                },
                EventData = new EventData
                {
                    NewData = person
                }
            };
        }

        public EntityEventSns Update(UpdateEntityResult<PersonDbEntity> updateResult, Token token)
        {
            return new EntityEventSns
            {
                CorrelationId = Guid.NewGuid(),
                DateTime = DateTime.UtcNow,
                EntityId = updateResult.UpdatedEntity.Id,
                Id = Guid.NewGuid(),
                EventType = PersonEventConstants.PERSON_UPDATED_EVENT,
                Version = PersonEventConstants.V1_VERSION,
                SourceDomain = PersonEventConstants.SOURCE_DOMAIN,
                SourceSystem = PersonEventConstants.SOURCE_SYSTEM,
                User = new User
                {
                    Name = token.Name,
                    Email = token.Email
                },
                EventData = new EventData
                {
                    OldData = updateResult.OldValues,
                    NewData = updateResult.NewValues
                }
            };
        }
    }
}
