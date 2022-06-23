using Hackney.Shared.Tenure.Boundary.Requests;
using Hackney.Shared.Tenure.Infrastructure;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Infrastructure;

namespace ProcessesApi.V1.Factories
{
    public static class EntityFactory
    {
        public static Process ToDomain(this ProcessesDb entity)
        {
            Process soleToJointProcess;
            soleToJointProcess = Process.Create(
                    entity.Id,
                    entity.PreviousStates,
                    entity.CurrentState,
                    entity.TargetId,
                    entity.TargetType,
                    entity.RelatedEntities,
                    entity.ProcessName,
                    entity.VersionNumber);
            return soleToJointProcess;
        }

        public static ProcessesDb ToDatabase(this Process entity)
        {
            return new ProcessesDb
            {
                Id = entity.Id,
                TargetId = entity.TargetId,
                TargetType = entity.TargetType,
                RelatedEntities = entity.RelatedEntities,
                ProcessName = entity.ProcessName,
                CurrentState = entity.CurrentState,
                PreviousStates = entity.PreviousStates,
                VersionNumber = entity.VersionNumber
            };
        }


    }
}
