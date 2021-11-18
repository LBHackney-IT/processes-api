using FluentValidation;
using Hackney.Core.Validation;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Boundary.Constants;
using System;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class CreateProcessQueryValidator : AbstractValidator<CreateProcessQuery>
    {
        public CreateProcessQueryValidator()
        {
            RuleFor(x => x.TargetId).NotNull()
                            .NotEqual(Guid.Empty);
            RuleForEach(x => x.RelatedEntities).NotNull()
                            .NotEqual(Guid.Empty);
            RuleForEach(x => x.Documents).NotNull()
                            .NotEqual(Guid.Empty);
        }
    }
}
