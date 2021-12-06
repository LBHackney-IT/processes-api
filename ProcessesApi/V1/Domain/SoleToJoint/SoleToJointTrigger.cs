using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Domain.SoleToJoint
{
    public class SoleToJointTrigger
    {
        private SoleToJointTrigger(Guid id, Guid? targetId, string trigger, object formData, List<Guid> documents, List<Guid> relatedEntities)
        {
            Id = id;
            TargetId = targetId;
            Trigger = trigger;
            FormData = formData;
            RelatedEntities = relatedEntities;
            Documents = documents;
        }

        public Guid Id { get; private set; }
        public Guid? TargetId { get; private set; }
        public string Trigger { get; private set; }
        public object FormData { get; private set; }
        public List<Guid> Documents { get; private set; }
        public List<Guid> RelatedEntities { get; private set; }

        public static SoleToJointTrigger Create(Guid id, Guid? targetId, string trigger, object formData, List<Guid> documents, List<Guid> relatedEntities)
        {
            return new SoleToJointTrigger(id, targetId, trigger, formData, documents, relatedEntities);
        }
    }
}
