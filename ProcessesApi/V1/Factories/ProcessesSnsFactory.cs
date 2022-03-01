using Hackney.Core.JWT;
using Hackney.Core.Sns;
using ProcessesApi.V1.Domain;
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

        public EntityEventSns ProcessStopped(Process process, Token token)
        {
            return new EntityEventSns
            {
                CorrelationId = Guid.NewGuid(),
                DateTime = DateTime.UtcNow,
                EntityId = process.Id,
                Id = Guid.NewGuid(),
                EventType = ProcessStoppedEventConstants.EVENTTYPE,
                Version = ProcessStoppedEventConstants.V1_VERSION,
                SourceDomain = ProcessStoppedEventConstants.SOURCE_DOMAIN,
                SourceSystem = ProcessStoppedEventConstants.SOURCE_SYSTEM,
                EventData = new EventData
                {
                    NewData = "Automatic eligibility check failed - process closed."
                },
                User = new User { Name = token.Name, Email = token.Email }
            };
        }
    }
}
