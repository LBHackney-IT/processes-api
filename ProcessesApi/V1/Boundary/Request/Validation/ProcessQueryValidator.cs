using FluentValidation;
using Hackney.Core.Validation;
using ProcessesApi.V1.Boundary.Constants;
using System;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class ProcessQueryValidator : AbstractValidator<ProcessQuery>
    {
        public ProcessQueryValidator()
        {
            RuleFor(x => x.ProcessName).NotNull().NotEmpty();
            RuleFor(x => x.ProcessName).NotXssString()
                .WithErrorCode(ErrorCodes.XssCheckFailure)
                .When(x => !string.IsNullOrEmpty(x.ProcessName));
            RuleFor(x => x.Id).NotNull()
                            .NotEqual(Guid.Empty);
        }
    }
}
