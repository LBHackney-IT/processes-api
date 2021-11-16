using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Domain
{
    public class ProcessState
    {
        public string StateName { get; set; }
        public List<String> PermittedTriggers { get; set; }
        public Assignment Assignment { get; set; }
        public ProcessData ProcessData { get; set; }
        public DateTime? CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }
}
