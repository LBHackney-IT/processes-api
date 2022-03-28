using ProcessesApi.V1.Domain;
using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Boundary.Request
{
    public class UpdateProcessRequestObject
    {
        public Dictionary<string, object> FormData { get; set; }
        public List<Guid> Documents { get; set; }
    }
}
