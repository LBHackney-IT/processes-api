using FluentValidation.TestHelper;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Boundary.Request.Validation;
using System;
using Xunit;

namespace ProcessesApi.Tests.V1.Boundary.Validation
{
    public class GetByIdRequestValidatorTests
    {
        private readonly GetByIdRequestValidator _classUnderTest;

        public GetByIdRequestValidatorTests()
        {
            _classUnderTest = new GetByIdRequestValidator();
        }

        [Fact]
        public void RequestShouldErrorWithNullId()
        {
            //Arrange
            var query = new ProcessesQuery();
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void RequestShouldErrorWithEmptyId()
        {
            //Arrange
            var query = new ProcessesQuery() { Id = Guid.Empty };
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }
    }
}
