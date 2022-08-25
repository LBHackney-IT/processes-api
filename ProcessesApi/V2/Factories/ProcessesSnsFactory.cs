using Hackney.Core.JWT;
using Hackney.Core.Sns;
using ProcessesApi.V2.Domain;
using ProcessesApi.V2.Infrastructure;
using ProcessesApi.V2.Infrastructure.JWT;
using System;
using System.Collections.Generic;

namespace ProcessesApi.V2.Factories
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
                EventType = ProcessEventConstants.PROCESS_STARTED_EVENT,
                Version = ProcessEventConstants.V1_VERSION,
                SourceDomain = ProcessEventConstants.SOURCE_DOMAIN,
                SourceSystem = ProcessEventConstants.SOURCE_SYSTEM,
                EventData = new EventData
                {
                    NewData = process
                },
                User = new User { Name = token.Name, Email = token.Email }
            };
        }

        public EntityEventSns ProcessStartedAgainstEntity(Process process, Token token, string eventType)
        {
            return new EntityEventSns
            {
                CorrelationId = Guid.NewGuid(),
                DateTime = DateTime.UtcNow,
                EntityId = process.TargetId,
                Id = Guid.NewGuid(),
                EventType = eventType,
                Version = ProcessEventConstants.V1_VERSION,
                SourceDomain = ProcessEventConstants.SOURCE_DOMAIN,
                SourceSystem = ProcessEventConstants.SOURCE_SYSTEM,
                EventData = new EventData
                {
                    NewData = new ProcessStartedAgainstEntityData
                    {
                        Id = process.Id,
                        ProcessName = process.ProcessName
                    }
                },
                User = new User { Name = token.Name, Email = token.Email }
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
                EventType = ProcessEventConstants.PROCESS_UPDATED_EVENT,
                Version = ProcessEventConstants.V1_VERSION,
                SourceDomain = ProcessEventConstants.SOURCE_DOMAIN,
                SourceSystem = ProcessEventConstants.SOURCE_SYSTEM,
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

        public EntityEventSns ProcessStateUpdated(Stateless.StateMachine<string, string>.Transition transition,
                                                  Dictionary<string, object> stateData,
                                                  Token token,
                                                  string eventType)
        {
            var triggerData = transition.Parameters[0] as ProcessTrigger;

            return new EntityEventSns
            {
                CorrelationId = Guid.NewGuid(),
                DateTime = DateTime.UtcNow,
                EntityId = triggerData.Id,
                Id = Guid.NewGuid(),
                EventType = eventType,
                Version = ProcessEventConstants.V1_VERSION,
                SourceDomain = ProcessEventConstants.SOURCE_DOMAIN,
                SourceSystem = ProcessEventConstants.SOURCE_SYSTEM,
                EventData = new EventData
                {
                    OldData = new ProcessStateChangeData
                    {
                        State = transition.Source
                    },
                    NewData = new ProcessStateChangeData
                    {
                        State = transition.Destination,
                        StateData = stateData
                    }
                },
                User = new User
                {
                    Name = token.Name,
                    Email = token.Email
                }
            };
        }
    }

    public class ProcessStateChangeData
    {
        public string State { get; set; }
        public Dictionary<string, object> StateData { get; set; }
    }

    public class ProcessStartedAgainstEntityData
    {
        public Guid Id { get; set; }
        public ProcessName ProcessName { get; set; }
    }
}
