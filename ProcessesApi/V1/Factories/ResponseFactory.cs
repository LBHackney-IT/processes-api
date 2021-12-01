using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.SoleToJoint;

namespace ProcessesApi.V1.Factories
{
    public static class ResponseFactory
    {
        public static ProcessResponse ToResponse(this SoleToJointProcess domain)
        {
            if (domain == null) return null;
            return new ProcessResponse
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
