using FluentValidation;
using System;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class GetProcessesByTargetIdRequestValidator : AbstractValidator<GetProcessesByTargetIdRequest>
    {
        public GetProcessesByTargetIdRequestValidator()
        {
            RuleFor(x => x.TargetId).NotNull().NotEqual(Guid.Empty);
        }
    }
}
