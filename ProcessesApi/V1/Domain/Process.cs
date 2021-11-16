using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Domain
{
    public class Process
    {
        public Guid Id { get; set; }
        public Guid TargetId { get; set; }
        public List<Guid> RelatedEntities { get; set; }
        public string ProcessName { get; set; }
        public ProcessState CurrentState { get; set; }
        public List<ProcessState> PreviousStates { get; set; }
        public int? VersionNumber { get; set; }
    }
}
