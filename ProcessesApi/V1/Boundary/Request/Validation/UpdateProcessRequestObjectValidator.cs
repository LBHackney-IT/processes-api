using FluentValidation;
using System;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class UpdateProcessRequestObjectValidator : AbstractValidator<UpdateProcessRequestObject>
    {
        public UpdateProcessRequestObjectValidator()
        {
            RuleFor(x => x.FormData).NotNull();
            RuleForEach(x => x.Documents).NotNull()
                                        .NotEqual(Guid.Empty);
        }
    }
}
