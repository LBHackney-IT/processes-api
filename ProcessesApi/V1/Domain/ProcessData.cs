using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Domain
{
    public class ProcessData
    {
        public Dictionary<string, object> FormData { get; set; }
        public List<Guid> Documents { get; set; }
        public ProcessData(Dictionary<string, object> formData, List<Guid> documents)
        {
            FormData = formData;
            Documents = documents;
        }

        public static ProcessData Create(Dictionary<string, object> formData, List<Guid> documents)
        {
            return new ProcessData(formData, documents);
        }
    }
}
