using Microsoft.AspNetCore.Mvc;
using System;

namespace ProcessesApi.V1.Boundary.Request
{
    public class ProcessQuery
    {
        [FromRoute(Name = "processName")]
        public string ProcessName { get; set; }
        [FromRoute(Name = "id")]
        public Guid Id { get; set; }
    }
}
