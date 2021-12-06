using FluentValidation;
using Hackney.Core.Validation;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Domain.Enums;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class ProcessStateValidator : AbstractValidator<ProcessState>
    {
        public ProcessStateValidator()
        {
            RuleFor(x => x.State).NotXssString()
                         .WithErrorCode(ErrorCodes.XssCheckFailure);
            RuleForEach(x => x.PermittedTriggers).NotXssString()
                         .WithErrorCode(ErrorCodes.XssCheckFailure);
            RuleFor(x => x.Assignment).SetValidator(new AssignmentValidator());
            RuleFor(x => x.ProcessData).SetValidator(new ProcessDataValidator());
        }
    }
}
