using FluentValidation.TestHelper;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Boundary.Request.Validation;
using System;
using Xunit;

namespace ProcessesApi.Tests.V1.Boundary.Validation
{
    public class ProcessQueryValidatorTests
    {
        private readonly ProcessQueryValidator _classUnderTest;
        private const string ValueWithTags = "sdfsdf<sometag>";


        public ProcessQueryValidatorTests()
        {
            _classUnderTest = new ProcessQueryValidator();
        }


        [Fact]
        public void RequestShouldErrorWithNullProcessName()
        {
            //Arrange
            var model = new ProcessQuery() { ProcessName = null };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.ProcessName);
        }

        [Fact]
        public void RequestShouldNotErrorWithValidProcessName()
        {
            //Arrange
            string processName = "process12345";
            var model = new ProcessQuery() { ProcessName = processName };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ProcessName);
        }

        [Fact]
        public void RequestShouldErrorWithTagsInProcessName()
        {
            var model = new ProcessQuery() { ProcessName = ValueWithTags };
            var result = _classUnderTest.TestValidate(model);
            result.ShouldHaveValidationErrorFor(x => x.ProcessName)
                .WithErrorCode(ErrorCodes.XssCheckFailure);
        }

        [Fact]
        public void RequestShouldErrorWithNullId()
        {
            //Arrange
            var query = new ProcessQuery();
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void RequestShouldErrorWithEmptyId()
        {
            //Arrange
            var query = new ProcessQuery() { Id = Guid.Empty };
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }
    }
}
