using FluentValidation;
using Hackney.Core.Validation;
using ProcessesApi.V1.Domain;
using System;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class ProcessValidator : AbstractValidator<Process>
    {
        public ProcessValidator()
        {
            RuleFor(x => x.Id).NotNull()
                .NotEqual(Guid.Empty);
            RuleFor(x => x.TargetId).NotNull()
                .NotEqual(Guid.Empty);
            RuleForEach(x => x.RelatedEntities).NotXssString()
                         .WithErrorCode(ErrorCodes.XssCheckFailure);
            RuleFor(x => x.ProcessName).NotXssString()
                         .WithErrorCode(ErrorCodes.XssCheckFailure);
            RuleFor(x => x.CurrentState).SetValidator(new ProcessStateValidator());
            RuleForEach(x => x.PreviousStates).SetValidator(new ProcessStateValidator());
        }
    }
}
