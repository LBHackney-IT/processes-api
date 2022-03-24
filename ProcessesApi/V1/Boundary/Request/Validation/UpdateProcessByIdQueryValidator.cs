using FluentValidation;
using System;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class UpdateProcessByIdQueryValidator : AbstractValidator<UpdateProcessByIdQuery>
    {
        public UpdateProcessByIdQueryValidator()
        {
            RuleFor(x => x.ProcessName).NotNull().NotEmpty();
            RuleFor(x => x.Id).NotNull()
                            .NotEqual(Guid.Empty);
        }
    }
}
