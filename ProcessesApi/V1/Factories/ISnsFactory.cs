using Hackney.Core.JWT;
using Hackney.Core.Sns;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Infrastructure;
using System;

namespace ProcessesApi.V1.Factories
{
    public interface ISnsFactory
    {
        EntityEventSns ProcessStarted(Process process, Token token);

        EntityEventSns ProcessClosed(Process process, Token token, string description);

        EntityEventSns ProcessUpdated(Process process, Token token, string description);
        EntityEventSns ProcessByIdUpdated(Guid id, UpdateEntityResult<ProcessState> updateResult, Token token);
    }
}
