using ProcessesApi.V1.Domain;
using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Boundary.Request
{
    public class UpdateProcessByIdRequestObject
    {
        public ProcessData ProcessData { get; set; }
        public Assignment Assignment { get; set; }

    }
}
