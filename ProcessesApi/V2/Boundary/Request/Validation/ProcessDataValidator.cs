using FluentValidation;
using ProcessesApi.V2.Domain;
using System;

namespace ProcessesApi.V2.Boundary.Request.Validation
{
    public partial class ProcessDataValidator : AbstractValidator<ProcessData>
    {
        public ProcessDataValidator()
        {
            RuleForEach(x => x.Documents).NotNull()
                                         .NotEqual(Guid.Empty);
        }
    }
}
