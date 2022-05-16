using FluentValidation;

namespace ProcessesApi.V1.Boundary.Request.Validation
{
    public class UpdateProcessByIdRequestObjectValidator : AbstractValidator<UpdateProcessByIdRequestObject>
    {
        public UpdateProcessByIdRequestObjectValidator()
        {
            RuleFor(x => x.ProcessData).SetValidator(new ProcessDataValidator());
        }
    }
}
