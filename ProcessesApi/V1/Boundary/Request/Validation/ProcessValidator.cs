using FluentValidation;
using Hackney.Core.Validation;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Boundary.Constants;
using System;
using ProcessesApi.V1.Domain.SoleToJoint;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class ProcessValidator : AbstractValidator<SoleToJointProcess>
    {
        public ProcessValidator()
        {
            RuleFor(x => x.Id).NotNull()
                .NotEqual(Guid.Empty);
            RuleFor(x => x.TargetId).NotNull()
                .NotEqual(Guid.Empty);
            RuleForEach(x => x.RelatedEntities).NotNull()
                         .NotEqual(Guid.Empty);
            RuleFor(x => x.ProcessName).NotXssString()
                         .WithErrorCode(ErrorCodes.XssCheckFailure);
            RuleFor(x => x.CurrentState).SetValidator(new ProcessStateValidator());
            RuleForEach(x => x.PreviousStates).SetValidator(new ProcessStateValidator());
        }
    }
}
