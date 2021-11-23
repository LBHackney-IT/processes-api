using FluentValidation;
using System;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class UpdateProcessQueryValidator : AbstractValidator<UpdateProcessQuery>
    {
        public UpdateProcessQueryValidator()
        {
            RuleFor(x => x.ProcessName).NotNull().NotEmpty();
            RuleFor(x => x.Id).NotNull()
                            .NotEqual(Guid.Empty);
            RuleFor(x => x.ProcessTrigger).NotNull().NotEmpty();
        }
    }
}
