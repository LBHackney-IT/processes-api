using System;
using System.Collections.Generic;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.Enums;

namespace ProcessesApi.V1.Boundary.Response
{
    public class ProcessResponse
    {
        public Guid Id { get; set; }
        public Guid TargetId { get; set; }
        public List<Guid> RelatedEntities { get; set; }
        public String ProcessName { get; set; }
        public ProcessState<SoleToJointStates, SoleToJointTriggers> CurrentState { get; set; }
        public List<ProcessState<SoleToJointStates, SoleToJointTriggers>> PreviousStates { get; set; }
    }
}
