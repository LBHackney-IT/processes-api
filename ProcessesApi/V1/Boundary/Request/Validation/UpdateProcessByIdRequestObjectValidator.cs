using FluentValidation;
using System;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class UpdateProcessByIdRequestObjectValidator : AbstractValidator<UpdateProcessByIdRequestObject>
    {
        public UpdateProcessByIdRequestObjectValidator()
        {
            RuleForEach(x => x.Documents).NotNull()
                                        .NotEqual(Guid.Empty);
        }
    }
}
