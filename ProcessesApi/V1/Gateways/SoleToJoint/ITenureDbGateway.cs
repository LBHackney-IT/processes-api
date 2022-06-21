using System;
using System.Threading.Tasks;
using Hackney.Shared.Tenure.Domain;

namespace ProcessesApi.V1.Gateways
{
    public interface ITenureDbGateway
    {
        Task<TenureInformation> GetTenureById(Guid id);
        Task UpdateTenureById(TenureInformation tenureInformation);
    }
}
