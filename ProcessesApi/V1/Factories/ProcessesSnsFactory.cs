using Hackney.Core.JWT;
using Hackney.Core.Sns;
using ProcessesApi.V1.Domain;
using System;
using ProcessesApi.V1.Infrastructure;

namespace ProcessesApi.V1.Factories
{
    public class ProcessesSnsFactory : ISnsFactory
    {
        public EntityEventSns Create(Process process, Token token, string eventType, object newData, object oldData = null)
        {
            return new EntityEventSns
            {
                CorrelationId = Guid.NewGuid(),
                DateTime = DateTime.UtcNow,
                EntityId = process.Id,
                Id = Guid.NewGuid(),
                EventType = eventType,
                Version = ProcessEventConstants.V1_VERSION,
                SourceDomain = ProcessEventConstants.SOURCE_DOMAIN,
                SourceSystem = ProcessEventConstants.SOURCE_SYSTEM,
                EventData = new EventData
                {
                    NewData = newData,
                    OldData = oldData
                },
                User = new User
                {
                    Name = token.Name,
                    Email = token.Email
                }
            };
        }
    }
}
