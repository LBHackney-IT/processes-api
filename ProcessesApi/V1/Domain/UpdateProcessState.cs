using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Domain
{
    public class UpdateProcessState
    {
        private UpdateProcessState(Guid id, Guid? targetId, string trigger, object formData, List<Guid> documents, List<Guid> relatedEntities)
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

        public static UpdateProcessState Create(Guid id, Guid? targetId, string trigger, object formData, List<Guid> documents, List<Guid> relatedEntities)
        {
            return new UpdateProcessState(id, targetId, trigger, formData, documents, relatedEntities);
        }
    }
}
