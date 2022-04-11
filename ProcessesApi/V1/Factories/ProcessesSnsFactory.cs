using Hackney.Core.JWT;
using Hackney.Core.Sns;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Infrastructure;
using ProcessesApi.V1.Infrastructure.JWT;
using System;

namespace ProcessesApi.V1.Factories
{
    public class ProcessesSnsFactory : ISnsFactory
    {
        public EntityEventSns ProcessStarted(Process process, Token token)
        {
            return new EntityEventSns
            {
                CorrelationId = Guid.NewGuid(),
                DateTime = DateTime.UtcNow,
                EntityId = process.Id,
                Id = Guid.NewGuid(),
                EventType = ProcessStartedEventConstants.EVENTTYPE,
                Version = ProcessStartedEventConstants.V1_VERSION,
                SourceDomain = ProcessStartedEventConstants.SOURCE_DOMAIN,
                SourceSystem = ProcessStartedEventConstants.SOURCE_SYSTEM,
                EventData = new EventData
                {
                    NewData = process
                },
                User = new User { Name = token.Name, Email = token.Email }
            };
        }

        public EntityEventSns ProcessClosed(Process process, Token token, string description)
        {
            return new EntityEventSns
            {
                CorrelationId = Guid.NewGuid(),
                DateTime = DateTime.UtcNow,
                EntityId = process.Id,
                Id = Guid.NewGuid(),
                EventType = ProcessClosedEventConstants.EVENTTYPE,
                Version = ProcessClosedEventConstants.V1_VERSION,
                SourceDomain = ProcessClosedEventConstants.SOURCE_DOMAIN,
                SourceSystem = ProcessClosedEventConstants.SOURCE_SYSTEM,
                EventData = new EventData
                {
                    NewData = description
                },
                User = new User
                {
                    Name = token.Name,
                    Email = token.Email
                }
            };
        }

        public EntityEventSns ProcessUpdatedWithMessage(Process process, Token token, string description)
        {
            return new EntityEventSns
            {
                CorrelationId = Guid.NewGuid(),
                DateTime = DateTime.UtcNow,
                EntityId = process.Id,
                Id = Guid.NewGuid(),
                EventType = ProcessUpdatedEventConstants.EVENTTYPE,
                Version = ProcessUpdatedEventConstants.V1_VERSION,
                SourceDomain = ProcessUpdatedEventConstants.SOURCE_DOMAIN,
                SourceSystem = ProcessUpdatedEventConstants.SOURCE_SYSTEM,
                EventData = new EventData
                {
                    NewData = description
                },
                User = new User
                {
                    Name = token.Name,
                    Email = token.Email
                }
            };
        }

        public EntityEventSns ProcessUpdated(Guid id, UpdateEntityResult<ProcessState> updateResult, Token token)
        {
            return new EntityEventSns
            {
                CorrelationId = Guid.NewGuid(),
                DateTime = DateTime.UtcNow,
                EntityId = id,
                Id = Guid.NewGuid(),
                EventType = ProcessUpdatedEventConstants.EVENTTYPE,
                Version = ProcessUpdatedEventConstants.V1_VERSION,
                SourceDomain = ProcessUpdatedEventConstants.SOURCE_DOMAIN,
                SourceSystem = ProcessUpdatedEventConstants.SOURCE_SYSTEM,
                EventData = new EventData
                {
                    OldData = updateResult.OldValues,
                    NewData = updateResult.NewValues
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
