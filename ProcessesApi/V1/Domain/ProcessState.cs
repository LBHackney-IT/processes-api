using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Domain
{
    public class ProcessState<TSt, TTr> where TSt : Enum where TTr : Enum
    {
        private readonly TSt _state;
        private readonly IList<TTr> _permittedTriggers;

        public ProcessState(TSt currentState, IList<TTr> permittedTriggers, Assignment assignment, ProcessData processData, DateTime createdAt, DateTime updatedAt)
        {
            _state = currentState;
            _permittedTriggers = permittedTriggers;

            Assignment = assignment;
            ProcessData = processData;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
        }

        public string State => _state.ToString();
        public IList<string> PermittedTriggers => _permittedTriggers.Select(x => x.ToString()).ToList();

        public Assignment Assignment { get; }
        public ProcessData ProcessData { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }


        [JsonIgnore]
        public TSt CurrentStateEnum => _state;


        public static ProcessState<TSt, TTr> Create(TSt currentState, IList<TTr> permittedTriggers, Assignment assignment, ProcessData processData, DateTime createdAt, DateTime updatedAt)
        {
            return new ProcessState<TSt, TTr>(currentState, permittedTriggers, assignment, processData, createdAt, updatedAt);
        }
    }
}
