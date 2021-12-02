using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Domain
{
    public class ProcessData
    {
        public object FormData { get; set; }
        public List<Guid> Documents { get; set; }
        public ProcessData(object formData, List<Guid> documents)
        {
            FormData = formData;
            Documents = documents;
        }

        public static ProcessData Create(object formData, List<Guid> documents)
        {
            return new ProcessData(formData, documents);
        }
    }
}
