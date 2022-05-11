using Hackney.Core.JWT;
using Hackney.Core.Sns;
using ProcessesApi.V1.Domain;

namespace ProcessesApi.V1.Factories
{
    public interface ISnsFactory
    {
        EntityEventSns Create(Process process, Token token, string eventType, object newData, object oldData = null);
    }
}
