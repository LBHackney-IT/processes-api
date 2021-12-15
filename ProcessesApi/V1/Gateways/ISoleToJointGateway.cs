using System;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Gateways
{
    public interface ISoleToJointGateway
    {
        Task<bool> CheckEligibility(Guid tenureId, Guid incomingTenantId);
    }
}
