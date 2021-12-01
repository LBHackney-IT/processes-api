using FluentValidation.TestHelper;
using ProcessesApi.V1.Boundary.Request.Validation;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Domain;
using Xunit;
using AutoFixture;

namespace ProcessesApi.Tests.V1.Boundary.Validation
{
    public class AssignmentValidatorTests
    {
        private readonly AssignmentValidator _classUnderTest;
        private readonly Fixture _fixture = new Fixture();

        public AssignmentValidatorTests()
        {
            _classUnderTest = new AssignmentValidator();
        }

        private const string StringWithTags = "Some string with <tag> in it.";

        [Fact]
        public void RequestShouldErrorWithTagsInType()
        {
            //Arrange
            var patch = "MMH";
            var model = new Assignment(patch) { Type = StringWithTags };
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
            var patch = "MMH";
            var model = new Assignment(patch) { Type = type };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Type);
        }

        [Fact]
        public void RequestShouldErrorWithTagsInValue()
        {
            //Arrange
            var patch = "MMH";
            var model = new Assignment(patch) { Value = StringWithTags };
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
            var patch = "MMH";
            var model = new Assignment(patch) { Value = value };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Value);
        }
    }
}
