using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProcessesApi.V1.Gateways;

namespace ProcessesApi.V1.Helpers
{
    public interface ISoleToJointHelper
    {
        public Task<bool> CheckAutomatedEligibility(Guid tenureId, Guid proposedTenantId, Guid tenantId, ISoleToJointGateway gateway);
    }
}
