using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Domain
{
    public class ProcessTrigger
    {
        private ProcessTrigger(Guid id, string trigger, Dictionary<string, object> formData, List<Guid> documents)
        {
            Id = id;
            Trigger = trigger;
            FormData = formData;
            Documents = documents;
        }

        public Guid Id { get; private set; }
        public string Trigger { get; set; }
        public Dictionary<string, Object> FormData { get; private set; }
        public List<Guid> Documents { get; private set; }

        public static ProcessTrigger Create(Guid id, string trigger, Dictionary<string, object> formData, List<Guid> documents)
        {
            return new ProcessTrigger(id, trigger, formData, documents);
        }
    }
}
