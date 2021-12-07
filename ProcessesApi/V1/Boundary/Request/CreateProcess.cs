using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Boundary.Request
{
    public class CreateProcess
    {
        public Guid TargetId { get; set; }
        public List<Guid> RelatedEntities { get; set; }
        public object FormData { get; set; }
        public List<Guid> Documents { get; set; }
    }
}
