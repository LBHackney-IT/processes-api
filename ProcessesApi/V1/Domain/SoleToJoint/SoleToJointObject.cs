using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Domain.SoleToJoint
{
    public class SoleToJointObject<T>
    {
        private SoleToJointObject(Guid id, T trigger, ProcessData processRequest)
        {
            Id = id;
            Trigger = trigger;
            ProcessRequest = processRequest;
        }

        public Guid Id { get; }
        public T Trigger { get; }
        public ProcessData ProcessRequest { get; }

        public static SoleToJointObject<T> Create(Guid id, T trigger, ProcessData processRequest)
        {
            return new SoleToJointObject<T>(id, trigger, processRequest);
        }
    }
}
