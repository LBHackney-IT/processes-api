using System;
using System.Collections.Generic;
using System.Text.Json;

namespace ProcessesApi.V1.Boundary.Request
{
    public class CreateProcess
    {
        public Guid TargetId { get; set; }
        public List<Guid> RelatedEntities { get; set; }
        public JsonElement FormData { get; set; }
        public List<Guid> Documents { get; set; }
    }
}
