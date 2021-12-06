using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.Enums;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Infrastructure;
using System.Collections.Generic;
using System.Linq;

namespace ProcessesApi.V1.Factories
{
    public static class EntityFactory
    {
        public static SoleToJointProcess ToDomain(this ProcessesDb entity)
        {
            SoleToJointProcess soleToJointProcess;
            soleToJointProcess = SoleToJointProcess.Create(
                    entity.Id,
                    entity.PreviousStates?.Select( x => x.ConvertStringToEnum<SoleToJointStates, SoleToJointTriggers>()).ToList(),
                    entity.CurrentState?.ConvertStringToEnum<SoleToJointStates, SoleToJointTriggers>(),
                    entity.TargetId,
                    entity.RelatedEntities,
                    entity.ProcessName,
                    entity.VersionNumber);
            return soleToJointProcess;
        }

        public static ProcessesDb ToDatabase(this SoleToJointProcess entity)
        {
            return new ProcessesDb
            {
                Id = entity.Id,
                TargetId = entity.TargetId,
                RelatedEntities = entity.RelatedEntities,
                ProcessName = entity.ProcessName,
                CurrentState = entity.CurrentState?.ConvertEnumsToString(),
                PreviousStates = entity.PreviousStates?.Select(x => x.ConvertEnumsToString()).ToList(),
                VersionNumber = entity.VersionNumber
            };
        }
    }
}
