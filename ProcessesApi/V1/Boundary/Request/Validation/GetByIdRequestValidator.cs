using FluentValidation;
using System;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class GetByIdRequestValidator : AbstractValidator<ProcessesQuery>
    {
        public GetByIdRequestValidator()
        {
            RuleFor(x => x.Id).NotNull()
                              .NotEqual(Guid.Empty);
        }
    }
}
