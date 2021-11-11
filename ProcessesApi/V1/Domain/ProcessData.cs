using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Domain
{
    public class ProcessData
    {
        public FormData FormData { get; set; }
        public List<string> Documents { get; set; }
    }
}