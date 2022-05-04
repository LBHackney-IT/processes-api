using System;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Helpers
{
    public interface ISoleToJointAutomatedEligibilityChecksHelper
    {
        public Task<bool> CheckAutomatedEligibility(Guid tenureId, Guid proposedTenantId, Guid tenantId);
    }
}
