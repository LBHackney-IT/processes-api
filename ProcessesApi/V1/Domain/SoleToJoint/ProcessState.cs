using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Domain.SoleToJoint
{
    public class ProcessState<TSt, TTr>
    {
        private readonly TSt _state;
        private readonly IList<TTr> _permittedTriggers;

        public ProcessState(TSt currentState, IList<TTr> permittedTriggers, Assignment assignment, ProcessData processData)
        {
            _state = currentState;
            _permittedTriggers = permittedTriggers;

            Assignment = assignment;
            ProcessData = processData;
        }

        public string State => _state.ToString();
        public IList<string> PermittedTriggers => _permittedTriggers.Select(x => x.ToString()).ToList();

        public Assignment Assignment { get; }
        public ProcessData ProcessData { get; }

        [JsonIgnore]
        public TSt CurrentStateEnum => _state;


        public static ProcessState<TSt, TTr> Create(TSt currentState, IList<TTr> permittedTriggers, Assignment assignment, ProcessData processData)
        {
            return new ProcessState<TSt, TTr>(currentState, permittedTriggers, assignment, processData);
        }
    }
}
