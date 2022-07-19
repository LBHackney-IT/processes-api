using FluentValidation;
using System;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class CreateProcessQueryValidator : AbstractValidator<CreateProcess>
    {
        public CreateProcessQueryValidator()
        {
            RuleFor(x => x.TargetId).NotNull()
                                    .NotEqual(Guid.Empty);
            RuleFor(x => x.TargetType).NotNull();
            RuleFor(x => x.RelatedEntities).NotNull();
            RuleForEach(x => x.RelatedEntities).SetValidator(new RelatedEntityValidator());
            RuleForEach(x => x.Documents).NotNull()
                                         .NotEqual(Guid.Empty);
        }
    }
}
