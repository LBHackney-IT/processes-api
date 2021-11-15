using FluentValidation;
using Hackney.Core.Validation;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Boundary.Constants;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class ProcessDataValidator : AbstractValidator<ProcessData>
    {
        public ProcessDataValidator()
        {
            RuleForEach(x => x.Documents).NotXssString()
                         .WithErrorCode(ErrorCodes.XssCheckFailure);
        }
    }
}
