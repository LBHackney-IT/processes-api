using FluentValidation;
using System;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class UpdateProcessQueryObjectValidator : AbstractValidator<UpdateProcessQueryObject>
    {
        public UpdateProcessQueryObjectValidator()
        {
            RuleForEach(x => x.Documents).NotNull()
                                        .NotEqual(Guid.Empty);
        }
    }
}
