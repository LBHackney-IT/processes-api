using System.Collections.Generic;
using System.Linq;
using ProcessesApi.V2.Boundary.Response;
using ProcessesApi.V2.Domain;

namespace ProcessesApi.V2.Factories
{
    public static class ResponseFactory
    {
        public static ProcessResponse ToResponse(this Process domain)
        {
            if (domain == null) return null;
            return new ProcessResponse
            {
                Id = domain.Id,
                TargetId = domain.TargetId,
                TargetType = domain.TargetType,
                RelatedEntities = domain.RelatedEntities,
                PatchAssignmentEntity = domain.PatchAssignmentEntity,
                ProcessName = domain.ProcessName,
                CurrentState = domain.CurrentState,
                PreviousStates = domain.PreviousStates
            };
        }

        public static List<ProcessResponse> ToResponse(this IEnumerable<Process> domainList)
        {
            if (domainList is null) return new List<ProcessResponse>();

            return domainList.Select(domain => domain.ToResponse()).ToList();
        }
    }
}
