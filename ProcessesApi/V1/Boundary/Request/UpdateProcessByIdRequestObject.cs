using ProcessesApi.V1.Domain;
using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Boundary.Request
{
    public class UpdateProcessByIdRequestObject
    {
        public Dictionary<string, object> FormData { get; set; }
        public List<Guid> Documents { get; set; }
        public Assignment Assignment { get; set; }

    }
}
