using FluentValidation.TestHelper;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Boundary.Request.Validation;
using System;
using System.Collections.Generic;
using Xunit;

namespace ProcessesApi.Tests.V1.Boundary.Validation
{
    public class UpdateProcessQueryObjectValidatorTests
    {
        private readonly UpdateProcessQueryObjectValidator _classUnderTest;

        public UpdateProcessQueryObjectValidatorTests()
        {
            _classUnderTest = new UpdateProcessQueryObjectValidator();
        }

        [Fact]
        public void RequestShouldErrorWithEmptyDocumentIDs()
        {
            //Arrange
            var model = new UpdateProcessQueryObject() { Documents = new List<Guid> { Guid.Empty } };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.Documents);
        }
        [Fact]
        public void RequestShouldNotErrorWithValidDocumentIDs()
        {
            //Arrange
            var model = new UpdateProcessQueryObject() { Documents = new List<Guid> { Guid.NewGuid() } };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Documents);
        }

    }
}
