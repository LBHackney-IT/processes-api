using FluentValidation;
using System;

namespace ProcessesApi.V2.Boundary.Request.Validation
{
    public partial class ProcessQueryValidator : AbstractValidator<ProcessQuery>
    {
        public ProcessQueryValidator()
        {
            RuleFor(x => x.Id).NotNull()
                              .NotEqual(Guid.Empty);
        }
    }
}
