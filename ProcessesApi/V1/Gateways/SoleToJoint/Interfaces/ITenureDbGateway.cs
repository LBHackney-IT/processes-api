using System;
using System.Threading.Tasks;
using Hackney.Shared.Tenure.Boundary.Requests;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Infrastructure;
using ProcessesApi.V1.Infrastructure;

namespace ProcessesApi.V1.Gateways
{
    public interface ITenureDbGateway
    {
        Task<TenureInformation> GetTenureById(Guid id);
        Task<UpdateEntityResult<TenureInformationDb>> UpdateTenureById(Guid id, EditTenureDetailsRequestObject updateTenureRequest);
        Task<TenureInformationDb> PostNewTenureAsync(CreateTenureRequestObject createTenureRequestObject);
    }
}
