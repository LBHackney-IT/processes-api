using Hackney.Shared.Processes.Domain;
using ProcessesApi.V1.Infrastructure;

namespace ProcessesApi.V1.Factories

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
                ProcessName = entity.ProcessName,
                PatchAssignment = entity.PatchAssignment,
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
                ProcessName = entity.ProcessName,
                PatchAssignment = entity.PatchAssignment,
                CurrentState = entity.CurrentState,
                PreviousStates = entity.PreviousStates,
                VersionNumber = entity.VersionNumber
            };
        }


    }
}
