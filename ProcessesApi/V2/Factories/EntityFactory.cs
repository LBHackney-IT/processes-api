using ProcessesApi.V2.Domain;
using ProcessesApi.V2.Infrastructure;

namespace ProcessesApi.V2.Factories
{
    public static class EntityFactory
    {
        public static Process ToDomain(this ProcessesDb entity)
        {
            return new Process
            {
                Id = entity.Id,
                TargetId = entity.TargetId,
                TargetType = entity.TargetType,
                RelatedEntities = entity.RelatedEntities,
                PatchAssignmentEntity = entity.PatchAssignmentEntity,
                ProcessName = entity.ProcessName,
                CurrentState = entity.CurrentState,
                PreviousStates = entity.PreviousStates,
                VersionNumber = entity.VersionNumber
            };
        }

        public static ProcessesDb ToDatabase(this Process entity)
        {
            return new ProcessesDb
            {
                Id = entity.Id,
                TargetId = entity.TargetId,
                TargetType = entity.TargetType,
                RelatedEntities = entity.RelatedEntities,
                PatchAssignmentEntity = entity.PatchAssignmentEntity,
                ProcessName = entity.ProcessName,
                CurrentState = entity.CurrentState,
                PreviousStates = entity.PreviousStates,
                VersionNumber = entity.VersionNumber
            };
        }


    }
}
