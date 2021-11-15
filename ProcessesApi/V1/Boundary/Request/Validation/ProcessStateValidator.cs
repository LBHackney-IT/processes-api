using FluentValidation;
using Hackney.Core.Validation;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Boundary.Constants;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class ProcessStateValidator : AbstractValidator<ProcessState>
    {
        public ProcessStateValidator()
        {
            RuleFor(x => x.StateName).NotXssString()
                         .WithErrorCode(ErrorCodes.XssCheckFailure);
            RuleForEach(x => x.PermittedTriggers).NotXssString()
                         .WithErrorCode(ErrorCodes.XssCheckFailure);
            RuleFor(x => x.Assignment).SetValidator(new AssignmentValidator());
            RuleFor(x => x.ProcessData).SetValidator(new ProcessDataValidator());
        }
    }
}
