using Hackney.Core.JWT;
using Hackney.Core.Sns;
using Hackney.Shared.Tenure.Infrastructure;
using ProcessesApi.V2.Infrastructure;

namespace ProcessesApi.V2.Factories
{
    public interface ITenureSnsFactory
    {
        EntityEventSns CreateTenure(TenureInformationDb tenure, Token token);
        EntityEventSns PersonAddedToTenure(UpdateEntityResult<TenureInformationDb> updateResult, Token token);
        EntityEventSns UpdateTenure(UpdateEntityResult<TenureInformationDb> updateResult, Token token);
        EntityEventSns PersonRemovedFromTenure(UpdateEntityResult<TenureInformationDb> updateResult, Token token);
    }
}
