using FluentValidation.TestHelper;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Boundary.Request.Validation;
using System;
using Xunit;

namespace ProcessesApi.Tests.V1.Boundary.Validation
{
    public class UpdateProcessByIdQueryValidatorTests
    {
        private readonly UpdateProcessByIdQueryValidator _classUnderTest;

        public UpdateProcessByIdQueryValidatorTests()
        {
            _classUnderTest = new UpdateProcessByIdQueryValidator();
        }

        [Fact]
        public void RequestShouldErrorWithNullProcessName()
        {
            //Arrange
            var model = new UpdateProcessByIdQuery() { ProcessName = null };
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
            var model = new UpdateProcessByIdQuery() { ProcessName = processName };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ProcessName);
        }

        [Fact]
        public void RequestShouldErrorWithNullId()
        {
            //Arrange
            var query = new UpdateProcessByIdQuery();
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void RequestShouldErrorWithEmptyId()
        {
            //Arrange
            var query = new UpdateProcessByIdQuery() { Id = Guid.Empty };
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }
    }
}
