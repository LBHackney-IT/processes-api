using FluentValidation.TestHelper;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Boundary.Request.Validation;
using System;
using Xunit;

namespace ProcessesApi.Tests.V1.Boundary.Validation
{
    public class GetProcessesByTargetIdRequestValidatorTests
    {
        private readonly GetProcessesByTargetIdRequestValidator _classUnderTest;

        public GetProcessesByTargetIdRequestValidatorTests()
        {
            _classUnderTest = new GetProcessesByTargetIdRequestValidator();
        }

        [Fact]
        public void RequestShouldErrorWithNullTargetId()
        {
            //Arrange
            var query = new GetProcessesByTargetIdRequest();
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.TargetId);
        }

        [Fact]
        public void RequestShouldErrorWithEmptyTargetId()
        {
            //Arrange
            var query = new GetProcessesByTargetIdRequest() { TargetId = Guid.Empty };
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.TargetId);
        }

        [Fact]
        public void RequestShouldNotErrorWithValidTargetId()
        {
            //Arrange
            var query = new GetProcessesByTargetIdRequest() { TargetId = Guid.NewGuid() };
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldNotHaveValidationErrorFor(x => x.TargetId);
        }
    }
}
