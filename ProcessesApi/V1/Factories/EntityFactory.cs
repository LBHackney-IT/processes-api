using System;
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
                Id = Guid.Parse(databaseEntity.Id),
                TargetId = Guid.Parse(databaseEntity.TargetId),
                RelatedEntities = databaseEntity.RelatedEntities,
                ProcessName = databaseEntity.ProcessName,
                CurrentState = databaseEntity.CurrentState,
                PreviousStates = databaseEntity.PreviousStates
            };
        }

        public static ProcessesDb ToDatabase(this Process entity)
        {

            return new ProcessesDb
            {
                Id = entity.Id.ToString(),
                TargetId = entity.TargetId.ToString(),
                RelatedEntities = entity.RelatedEntities,
                ProcessName = entity.ProcessName,
                CurrentState = entity.CurrentState,
                PreviousStates = entity.PreviousStates
            };
        }
    }
}
