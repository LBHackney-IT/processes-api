using FluentValidation;
using Hackney.Core.Validation;
using ProcessesApi.V1.Boundary.Constants;
using System;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class UpdateProcessQueryValidator : AbstractValidator<UpdateProcessQuery>
    {
        public UpdateProcessQueryValidator()
        {
            RuleFor(x => x.Id).NotNull()
                            .NotEqual(Guid.Empty);
            RuleFor(x => x.ProcessTrigger).NotNull().NotEmpty();
        }
    }
}
