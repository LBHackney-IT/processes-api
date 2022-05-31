using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Domain
{
    public class Process
    {
        public Guid Id { get; set; }

        public ProcessState CurrentState { get; set; }
        public List<ProcessState> PreviousStates { get; set; }
        public Guid TargetId { get; set; }
        public TargetType TargetType { get; set; }
        public List<RelatedEntities> RelatedEntities { get; set; }
        public ProcessName ProcessName { get; set; }
        public int? VersionNumber { get; set; }


        public Process(Guid id, List<ProcessState> previousStates,
            ProcessState currentState, Guid targetId, TargetType targetType,
            List<RelatedEntities> relatedEntities, ProcessName processName, int? versionNumber)
        {
            Id = id;
            CurrentState = currentState;
            PreviousStates = previousStates;
            TargetId = targetId;
            TargetType = targetType;
            RelatedEntities = relatedEntities;
            ProcessName = processName;
            VersionNumber = versionNumber;
        }

        public Task AddState(ProcessState updatedState)
        {
            if (CurrentState != null) PreviousStates.Add(CurrentState);
            CurrentState = updatedState;

            return Task.CompletedTask;
        }



        public static Process Create(Guid id,
           List<ProcessState> previousStates,
           ProcessState currentState, Guid targetId, TargetType targetType,
           List<RelatedEntities> relatedEntities, ProcessName processName, int? versionNumber)
        {

            return new Process(id, previousStates, currentState, targetId, targetType, relatedEntities, processName, versionNumber);
        }
    }
}
