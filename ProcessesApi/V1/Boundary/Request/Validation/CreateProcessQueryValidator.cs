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
            // Uncomment when frontend has added relatedEntities as part of the CreateProcess Request
            //RuleFor(x => x.RelatedEntities).NotNull();
            RuleForEach(x => x.Documents).NotNull()
                            .NotEqual(Guid.Empty);
        }
    }
}
