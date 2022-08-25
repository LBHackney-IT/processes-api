using Hackney.Core.JWT;
using Hackney.Core.Sns;
using Hackney.Shared.Person;
using Hackney.Shared.Person.Infrastructure;
using ProcessesApi.V2.Infrastructure;

namespace ProcessesApi.V2.Factories
{
    public interface IPersonSnsFactory
    {
        EntityEventSns Create(Person person, Token token);

        EntityEventSns Update(UpdateEntityResult<PersonDbEntity> updateResult, Token token);
    }
}
