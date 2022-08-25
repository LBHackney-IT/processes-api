using Microsoft.AspNetCore.Mvc;
using ProcessesApi.V2.Domain;
using System;

namespace ProcessesApi.V2.Boundary.Request
{
    public class ProcessQuery
    {
        [FromRoute(Name = "processName")]
        public ProcessName ProcessName { get; set; }
        [FromRoute(Name = "id")]
        public Guid Id { get; set; }
    }
}
