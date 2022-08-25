using FluentValidation;
using ProcessesApi.V2.Domain;
using System;

namespace ProcessesApi.V2.Boundary.Request.Validation
{
    public class RelatedEntityValidator : AbstractValidator<RelatedEntity>
    {
        public RelatedEntityValidator()
        {
            RuleFor(x => x.Id).NotNull().NotEqual(Guid.Empty);
            RuleFor(x => x.TargetType).NotNull();
        }
    }
}
