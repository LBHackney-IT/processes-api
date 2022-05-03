using Microsoft.AspNetCore.Mvc;
using System;

namespace ProcessesApi.V1.Boundary.Request
{
    public class UpdateProcessQuery
    {
        [FromRoute(Name = "processName")]
        public string ProcessName { get; set; }
        [FromRoute(Name = "id")]
        public Guid Id { get; set; }
        [FromRoute(Name = "processTrigger")]
        public string ProcessTrigger { get; set; }
    }
}
