using System;

namespace ProcessesApi.V2.Gateways.Exceptions
{
    public class TenureNotFoundException : EntityNotFoundException
    {
        public TenureNotFoundException(Guid id)
            : base("Tenure", id)
        { }
    }
}
