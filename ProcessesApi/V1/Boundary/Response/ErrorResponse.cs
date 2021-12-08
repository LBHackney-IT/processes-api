using System;

namespace ProcessesApi.V1.Boundary.Response
{
    public class ErrorResponse
    {
        public int ErrorCode { get; set; }
        public string ErrorMessage { get; set; }
        public Guid ProcessId { get; set; }
        public string ProcessName { get; set; }
    }
}
