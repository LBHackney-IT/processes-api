using FluentValidation;
using System;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class ProcessQueryValidator : AbstractValidator<ProcessQuery>
    {
        public ProcessQueryValidator()
        {
            RuleFor(x => x.Id).NotNull()
                            .NotEqual(Guid.Empty);
        }
    }
}
