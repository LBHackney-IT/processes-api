using FluentValidation;
using ProcessesApi.V1.Domain;
using System;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class ProcessDataValidator : AbstractValidator<ProcessData>
    {
        public ProcessDataValidator()
        {
            RuleForEach(x => x.Documents).NotNull()
                                         .NotEqual(Guid.Empty);
        }
    }
}
