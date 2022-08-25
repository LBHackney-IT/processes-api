using ProcessesApi.V2.Domain;
using System;
using System.Collections.Generic;

namespace ProcessesApi.V2.Boundary.Request
{
    public class CreateProcess
    {
        public Guid TargetId { get; set; }
        public TargetType TargetType { get; set; }
        public List<RelatedEntity> RelatedEntities { get; set; }
        public PatchAssignmentEntity PatchAssignmentEntity { get; set; }
        public Dictionary<string, object> FormData { get; set; }
        public List<Guid> Documents { get; set; }
    }
}
