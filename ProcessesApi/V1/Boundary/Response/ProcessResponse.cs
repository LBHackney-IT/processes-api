using System;
using System.Collections.Generic;
using ProcessesApi.V1.Domain;

namespace ProcessesApi.V1.Boundary.Response
{
    public class ProcessResponse
    {
        public Guid Id { get; set; }
        public Guid TargetId { get; set; }
        public List<Guid> RelatedEntities { get; set; }
        public String ProcessName { get; set; }
        public ProcessState CurrentState { get; set; }
        public List<ProcessState> PreviousStates { get; set; }
    }
}
