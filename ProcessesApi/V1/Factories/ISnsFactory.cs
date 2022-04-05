using Hackney.Core.JWT;
using Hackney.Core.Sns;
using ProcessesApi.V1.Domain;

namespace ProcessesApi.V1.Factories
{
    public interface ISnsFactory
    {
        EntityEventSns ProcessStarted(Process process, Token token);

        EntityEventSns ProcessClosed(Process process, Token token, string description);

        EntityEventSns ProcessUpdated(Process process, Token token, string description);
        EntityEventSns ProcessByIdUpdated(Process old, Process updated, Token token);
    }
}
