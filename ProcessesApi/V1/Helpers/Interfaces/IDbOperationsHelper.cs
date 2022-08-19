using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hackney.Core.JWT;
using Hackney.Shared.Person;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;

namespace ProcessesApi.V1.Helpers
{
    public interface IDbOperationsHelper
    {
        Task AddIncomingTenantToRelatedEntities(Dictionary<string, object> requestFormData, Process process);
        Task<bool> CheckAutomatedEligibility(Guid tenureId, Guid proposedTenantId, Guid tenantId);
        Task<Guid> UpdateTenures(Process process, Token token);
        Task UpdatePerson(Process process, Token token);
    }
}
