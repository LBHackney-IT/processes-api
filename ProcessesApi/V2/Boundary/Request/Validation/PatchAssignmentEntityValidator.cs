using FluentValidation;
using ProcessesApi.V2.Domain;
using System;

namespace ProcessesApi.V2.Boundary.Request.Validation
{
    public partial class PatchAssignmentEntityValidator : AbstractValidator<PatchAssignmentEntity>
    {
        public PatchAssignmentEntityValidator()
        {
            RuleFor(x => x.PatchId).NotNull().NotEqual(Guid.Empty);
            RuleFor(x => x.PatchName).NotNull();
            RuleFor(x => x.ResponsibleEntityId).NotNull().NotEqual(Guid.Empty);
            RuleFor(x => x.ResponsibleName).NotNull();
        }
    }
}
