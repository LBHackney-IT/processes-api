using AutoFixture;
using FluentValidation.TestHelper;
using ProcessesApi.V1.Boundary.Request.Validation;
using ProcessesApi.V1.Domain;
using System;
using System.Collections.Generic;
using Xunit;

namespace ProcessesApi.Tests.V1.Boundary.Validation
{
    public class ProcessDataValidatorTests
    {
        private readonly ProcessDataValidator _classUnderTest;
        private readonly Fixture _fixture = new Fixture();

        public ProcessDataValidatorTests()
        {
            _classUnderTest = new ProcessDataValidator();
        }

        [Fact]
        public void RequestShouldErrorWithEmptyDocumentIDs()
        {
            //Arrange
            var model = new ProcessData(new Dictionary<string, object>(), new List<Guid> { Guid.Empty });
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.Documents);
        }
        [Fact]
        public void RequestShouldNotErrorWithValidDocumentIDs()
        {
            //Arrange
            var model = new ProcessData(new Dictionary<string, object>(), new List<Guid> { Guid.NewGuid() });
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Documents);
        }

    }
}
