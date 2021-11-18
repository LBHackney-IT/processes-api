using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace ProcessesApi.V1.Boundary.Request
{
    public class CreateProcessQuery
    {
        public Guid Id { get; set; }
        public Guid TargetId { get; set; }
        public List<Guid> RelatedEntities { get; set; }
        public Object FormData { get; set; }
        public List<Guid> Documents { get; set; }
    }
}
