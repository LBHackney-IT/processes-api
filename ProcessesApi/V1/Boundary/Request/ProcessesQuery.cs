using System;
using Microsoft.AspNetCore.Mvc;

namespace ProcessesApi.V1.Boundary.Request
{
    public class ProcessesQuery
    {
        [FromRoute(Name = "id")]
        public Guid Id { get; set; }
    }
}
