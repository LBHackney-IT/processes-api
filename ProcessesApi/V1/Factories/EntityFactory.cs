using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Infrastructure;

namespace ProcessesApi.V1.Factories
{
    public static class EntityFactory
    {
        public static Process ToDomain(this ProcessesDb databaseEntity)
        {
            return new Process
            {
                Id = databaseEntity.Id,
                TargetId = databaseEntity.TargetId,
                RelatedEntities = databaseEntity.RelatedEntities,
                ProcessName = databaseEntity.ProcessName,
                CurrentState = databaseEntity.CurrentState,
                PreviousStates = databaseEntity.PreviousStates,
                VersionNumber = databaseEntity.VersionNumber
            };
        }

        public static ProcessesDb ToDatabase(this Process entity)
        {

            return new ProcessesDb
            {
                Id = entity.Id,
                TargetId = entity.TargetId,
                RelatedEntities = entity.RelatedEntities,
                ProcessName = entity.ProcessName,
                CurrentState = entity.CurrentState,
                PreviousStates = entity.PreviousStates,
                VersionNumber = entity.VersionNumber
            };
        }
    }
}
