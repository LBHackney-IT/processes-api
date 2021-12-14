using System;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Helpers
{
    public interface ISoleToJointHelper
    {
        Task<bool> CheckEligibility(Guid tenureId, Guid incomingTenantId);
    }
}
