using FluentValidation.TestHelper;
using ProcessesApi.V1.Boundary.Request.Validation;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Domain;
using Xunit;
using System.Collections.Generic;

namespace ProcessesApi.Tests.V1.Boundary.Validation
{
    public class ProcessDataValidatorTests
    {
        private readonly ProcessDataValidator _classUnderTest;

        public ProcessDataValidatorTests()
        {
            _classUnderTest = new ProcessDataValidator();
        }

        private const string StringWithTags = "Some string with <tag> in it.";

        [Fact]
        public void RequestShouldErrorWithTagsInDocument()
        {
            //Arrange
            var model = new ProcessData() { Documents = new List<string> { StringWithTags } };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.Documents)
                  .WithErrorCode(ErrorCodes.XssCheckFailure);
        }

        [Fact]
        public void RequestShouldNotErrorWithValidDocument()
        {
            //Arrange
            string document = "document12345";
            var model = new ProcessData() { Documents = new List<string> { document } };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldNotHaveValidationErrorFor(x => x.Documents);
        }
    }
}
