using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProcessesApi.V1.Domain;

namespace ProcessesApi.V1.Helpers
{
    public interface ISoleToJointDbOperationsHelper
    {
        Task AddIncomingTenantToRelatedEntities(Dictionary<string, object> requestFormData, Process process);
        Task<bool> CheckAutomatedEligibility(Guid tenureId, Guid proposedTenantId, Guid tenantId);
        Task<Guid> UpdateTenures(Process process);
    }
}
