using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Boundary.Request
{
    public class UpdateProcessQueryObject
    {
        public Object FormData { get; set; }
        public List<Guid> Documents { get; set; }
    }
}
