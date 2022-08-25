using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProcessesApi.V2.Domain
{
    public class Process
    {
        public Guid Id { get; set; }
        public Guid TargetId { get; set; }
        public TargetType TargetType { get; set; }
        public List<RelatedEntity> RelatedEntities { get; set; }
        public PatchAssignmentEntity PatchAssignmentEntity { get; set; }
        public ProcessName ProcessName { get; set; }
        public ProcessState CurrentState { get; set; }
        public List<ProcessState> PreviousStates { get; set; }
        public int? VersionNumber { get; set; }

        public Process() { }

        public Process(Guid id, Guid targetId, TargetType targetType, List<RelatedEntity> relatedEntities, ProcessName processName, ProcessState currentState, List<ProcessState> previousStates, PatchAssignmentEntity patchAssignmentEntity, int? versionNumber)
        {
            Id = id;
            TargetId = targetId;
            TargetType = targetType;
            RelatedEntities = relatedEntities;
            PatchAssignmentEntity = patchAssignmentEntity;
            ProcessName = processName;
            CurrentState = currentState;
            PreviousStates = previousStates;
            VersionNumber = versionNumber;
        }

        public Task AddState(ProcessState updatedState)
        {
            if (CurrentState != null) PreviousStates.Add(CurrentState);
            CurrentState = updatedState;

            return Task.CompletedTask;
        }

        public static Process Create(Guid targetId, TargetType targetType, List<RelatedEntity> relatedEntities, ProcessName processName, PatchAssignmentEntity patchAssignmentEntity)
        {

            return new Process(Guid.NewGuid(), targetId, targetType, relatedEntities, processName, null, new List<ProcessState>(), patchAssignmentEntity, null);
        }
    }
}
