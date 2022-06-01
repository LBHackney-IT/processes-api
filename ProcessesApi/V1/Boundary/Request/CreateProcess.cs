using ProcessesApi.V1.Domain;
using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Boundary.Request
{
    public class CreateProcess
    {
        public Guid TargetId { get; set; }
        public TargetType TargetType { get; set; }
        public List<RelatedEntity> RelatedEntities { get; set; }
        public Dictionary<string, object> FormData { get; set; }
        public List<Guid> Documents { get; set; }
    }
}
