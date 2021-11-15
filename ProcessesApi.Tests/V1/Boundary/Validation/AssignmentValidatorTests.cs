using FluentValidation.TestHelper;
using ProcessesApi.V1.Boundary.Request.Validation;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Domain;
using Xunit;

namespace ProcessesApi.Tests.V1.Boundary.Validation
{
    public class AssignmentValidatorTests
    {
        private readonly AssignmentValidator _classUnderTest;

        public AssignmentValidatorTests()
        {
            _classUnderTest = new AssignmentValidator();
        }

        private const string StringWithTags = "Some string with <tag> in it.";

        [Fact]
        public void RequestShouldErrorWithTagsInType()
        {
            //Arrange
            var model = new Assignment() { Type = StringWithTags };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.Type)
                  .WithErrorCode(ErrorCodes.XssCheckFailure);
        }

        [Fact]
        public void RequestShouldNotErrorWithValidType()
        {
            //Arrange
            string type = "type12345";
            var model = new Assignment() { Type = type };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Type);
        }

        [Fact]
        public void RequestShouldErrorWithTagsInValue()
        {
            //Arrange
            var model = new Assignment() { Value = StringWithTags };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.Value)
                  .WithErrorCode(ErrorCodes.XssCheckFailure);
        }

        [Fact]
        public void RequestShouldNotErrorWithValidValue()
        {
            //Arrange
            string value = "value12345";
            var model = new Assignment() { Value = value };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Value);
        }
    }
}
