using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Domain
{
    public class ProcessTrigger
    {
        private ProcessTrigger(Guid id, Guid? targetId, string trigger, Dictionary<string, object> formData, List<Guid> documents, List<Guid> relatedEntities)
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
        public string Trigger { get; set; }
        public Dictionary<string, Object> FormData { get; private set; }
        public List<Guid> Documents { get; private set; }
        public List<Guid> RelatedEntities { get; private set; }

        public static ProcessTrigger Create(Guid id, Guid? targetId, string trigger, Dictionary<string, object> formData, List<Guid> documents, List<Guid> relatedEntities)
        {
            return new ProcessTrigger(id, targetId, trigger, formData, documents, relatedEntities);
        }
    }
}
