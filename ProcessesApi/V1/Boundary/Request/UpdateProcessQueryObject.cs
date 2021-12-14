using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Boundary.Request
{
    public class UpdateProcessQueryObject
    {
        public Dictionary<string, object> FormData { get; set; }
        public List<Guid> Documents { get; set; }
    }
}
