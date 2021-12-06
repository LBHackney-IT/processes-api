using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Domain
{
    public class ProcessState<TSt, TTr>
    {
        private readonly TSt _state;
        private readonly IList<TTr> _permittedTriggers;

        public ProcessState(TSt currentState, IList<TTr> permittedTriggers, Assignment assignment, ProcessData processData, DateTime createdAt, DateTime updatedAt)
        {
            _state = currentState;
            _permittedTriggers = permittedTriggers;
            State = currentState?.ToString();
            PermittedTriggers = permittedTriggers?.Select(x => x.ToString()).ToList();
            Assignment = assignment;
            ProcessData = processData;
            CreatedAt = createdAt;
            UpdatedAt = updatedAt;
            CurrentStateEnum = currentState;
            
        }
        [JsonIgnore]
        public string State { get; set; }
        [JsonIgnore]
        public IList<string> PermittedTriggers { get; set; }

        public Assignment Assignment { get; set; }
        public ProcessData ProcessData { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime UpdatedAt { get; set; }


        [JsonIgnore]
        public TSt CurrentStateEnum { get; set; }


        public static ProcessState<TSt, TTr> Create(TSt currentState, IList<TTr> permittedTriggers, Assignment assignment, ProcessData processData, DateTime createdAt, DateTime updatedAt)
        {
            return new ProcessState<TSt, TTr>(currentState, permittedTriggers, assignment, processData, createdAt, updatedAt);
        }

        public ProcessState<string, string> ConvertEnumsToString()
        {

            return new ProcessState<string, string>(State, PermittedTriggers, Assignment, ProcessData, CreatedAt, UpdatedAt);
        }

        public ProcessState<TState, TTriggers> ConvertStringToEnum<TState, TTriggers>()
        {
            var state = (TState)Enum.Parse(typeof(TState), State);
            var permittedTriggers = PermittedTriggers?.Select(x => (TTriggers) Enum.Parse(typeof(TTriggers), x)).ToList();
            return new ProcessState<TState, TTriggers>(state, permittedTriggers, Assignment, ProcessData, CreatedAt, UpdatedAt);

        }
    }
}
