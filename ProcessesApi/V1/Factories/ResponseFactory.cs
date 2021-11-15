using System.Collections.Generic;
using System.Linq;
using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Domain;

namespace ProcessesApi.V1.Factories
{
    public static class ResponseFactory
    {
        public static ProcessesResponse ToResponse(this Process domain)
        {
            if (domain == null) return null;
            return new ProcessesResponse
            {
                Id = domain.Id,
                TargetId = domain.TargetId,
                RelatedEntities = domain.RelatedEntities,
                ProcessName = domain.ProcessName,
                CurrentState = domain.CurrentState,
                PreviousStates = domain.PreviousStates
            };
        }
    }
}
