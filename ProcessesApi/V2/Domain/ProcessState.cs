using System;
using System.Collections.Generic;
using ProcessesApi.V1.Domain;

namespace ProcessesApi.V2.Domain
{
    public class ProcessState
    {

        public ProcessState(string state, IList<string> permittedTriggers, Assignment assignment, ProcessData processData, DateTime createdAt, DateTime updatedAt)
        {
            State = state;
            PermittedTriggers = permittedTriggers;
            Assignment = assignment;
            ProcessData = processData;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public string State { get; set; }
        public IList<string> PermittedTriggers { get; set; }

        public Assignment Assignment { get; set; }
        public ProcessData ProcessData { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }

        public static ProcessState Create(string currentState, IList<string> permittedTriggers, Assignment assignment, ProcessData processData, DateTime createdAt, DateTime updatedAt)
        {
            return new ProcessState(currentState, permittedTriggers, assignment, processData, createdAt, updatedAt);
        }
    }
}
