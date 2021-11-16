using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Domain
{
    public class ProcessData
    {
        public Object FormData { get; set; }
        public List<Guid> Documents { get; set; }
    }
}
