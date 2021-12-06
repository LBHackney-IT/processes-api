using FluentValidation.TestHelper;
using ProcessesApi.V1.Boundary.Request.Validation;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Domain;
using Xunit;
using System.Collections.Generic;
using ProcessesApi.V1.Domain.Enums;
using AutoFixture;

namespace ProcessesApi.Tests.V1.Boundary.Validation
{
    public class ProcessStateValidatorTests
    {
        private readonly ProcessStateValidator _classUnderTest;
        private readonly Fixture _fixture = new Fixture();


        public ProcessStateValidatorTests()
        {
            _classUnderTest = new ProcessStateValidator();
        }

        private const string StringWithTags = "Some string with <tag> in it.";

        [Fact]
        public void RequestShouldErrorWithTagsInStateName()
        {
            //Arrange
            var model = _fixture.Build<ProcessState>()
                                .With(x => x.State, StringWithTags)
                                .Create();
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.State)
                  .WithErrorCode(ErrorCodes.XssCheckFailure);
        }

        [Fact]
        public void RequestShouldNotErrorWithValidStateName()
        {
            //Arrange
            string stateName = "name12345";
            var model = _fixture.Build<ProcessState>()
                               .With(x => x.State, stateName)
                               .Create();
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldNotHaveValidationErrorFor(x => x.State);
        }


    }
}
