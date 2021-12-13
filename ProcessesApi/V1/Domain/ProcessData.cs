using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ProcessesApi.V1.Domain
{
    public class ProcessData
    {
        public JsonElement FormData { get; set; }
        public List<Guid> Documents { get; set; }
        public ProcessData(JsonElement formData, List<Guid> documents)
        {
            FormData = formData;
            Documents = documents;
        }

        public static ProcessData Create(JsonElement formData, List<Guid> documents)
        {
            return new ProcessData(formData, documents);
        }
    }
}
