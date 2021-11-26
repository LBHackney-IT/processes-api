using ProcessesApi.V1.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Boundary.Request
{
    public class SoleToJointRequest
    {
        private SoleToJointRequest(Guid id, string trigger, ProcessRequest processRequest)
        {
            Id = id;
            Trigger = trigger;
            ProcessRequest = processRequest;
        }

        public Guid Id { get; }
        public string Trigger { get; }
        public ProcessRequest ProcessRequest { get; }

        public static SoleToJointRequest Create(Guid id, string trigger, ProcessRequest processRequest)
        {
            return new SoleToJointRequest(id, trigger, processRequest);
        }
    }
}
