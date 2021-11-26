using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ProcessesApi.V1.Domain
{
    public class ProcessRequest
    {
        public Guid TargetId { get; set; }
        public JsonElement FormData { get; set; }
        public List<Guid> Documents { get; set; }
    }
}
