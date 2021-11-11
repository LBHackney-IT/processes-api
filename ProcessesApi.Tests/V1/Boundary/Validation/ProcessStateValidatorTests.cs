using FluentValidation.TestHelper;
using ProcessesApi.V1.Boundary.Request.Validation;
using ProcessesApi.V1.Domain;
using Xunit;
using System.Collections.Generic;

namespace ProcessesApi.Tests.V1.Boundary.Validation
{
    public class ProcessStateValidatorTests
    {
        private readonly ProcessStateValidator _classUnderTest;

        public ProcessStateValidatorTests()
        {
            _classUnderTest = new ProcessStateValidator();
        }

        private const string StringWithTags = "Some string with <tag> in it.";

        [Fact]
        public void RequestShouldErrorWithTagsInStateName()
        {
            //Arrange
            var model = new ProcessState() { StateName = StringWithTags };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.StateName)
                  .WithErrorCode(ErrorCodes.XssCheckFailure);
        }

        [Fact]
        public void RequestShouldNotErrorWithValidStateName()
        {
            //Arrange
            string stateName = "name12345";
            var model = new ProcessState() { StateName = stateName };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldNotHaveValidationErrorFor(x => x.StateName);
        }

        [Fact]
        public void RequestShouldErrorWithTagsInPermittedTriggers()
        {
            //Arrange
            var model = new ProcessState() { PermittedTriggers = new List<string> { StringWithTags } };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.PermittedTriggers)
                  .WithErrorCode(ErrorCodes.XssCheckFailure);
        }

        [Fact]
        public void RequestShouldNotErrorWithValidPermittedTriggers()
        {
            //Arrange
            string permittedTrigger = "trigger12345";
            var model = new ProcessState() { PermittedTriggers = new List<string> { permittedTrigger } };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldNotHaveValidationErrorFor(x => x.StateName);
        }
    }
}
