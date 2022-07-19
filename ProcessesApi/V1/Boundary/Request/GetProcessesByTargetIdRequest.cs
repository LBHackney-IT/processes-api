using Microsoft.AspNetCore.Mvc;
using System;

namespace ProcessesApi.V1.Boundary.Request
{
    public class GetProcessesByTargetIdRequest
    {
        [FromQuery]
        public Guid TargetId { get; set; }

        [FromQuery]
        public string PaginationToken { get; set; }

        [FromQuery]
        public int? PageSize { get; set; }
    }
}
