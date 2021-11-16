using FluentValidation.TestHelper;
using ProcessesApi.V1.Boundary.Request.Validation;
using ProcessesApi.V1.Domain;
using Xunit;
using System;
using System.Collections.Generic;

namespace ProcessesApi.Tests.V1.Boundary.Validation
{
    public class ProcessValidatorTests
    {
        private readonly ProcessValidator _classUnderTest;

        public ProcessValidatorTests()
        {
            _classUnderTest = new ProcessValidator();
        }

        [Fact]
        public void RequestShouldErrorWithNullId()
        {
            //Arrange
            var query = new Process();
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void RequestShouldErrorWithEmptyId()
        {
            //Arrange
            var query = new Process() { Id = Guid.Empty };
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void RequestShouldErrorWithNullTargetId()
        {
            //Arrange
            var query = new Process();
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.TargetId);
        }

        [Fact]
        public void RequestShouldErrorWithEmptyTargetId()
        {
            //Arrange
            var query = new Process() { TargetId = Guid.Empty };
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.TargetId);
        }

        [Fact]
        public void RequestShouldErrorWithEmptyRelatedEntity()
        {
            //Arrange
            var query = new Process() { RelatedEntities = new List<Guid> { Guid.Empty } };
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.RelatedEntities);
        }

        [Fact]
        public void RequestShouldNotErrorWithValidProcessName()
        {
            //Arrange
            string processName = "process12345";
            var model = new Process() { ProcessName = processName };
            //Act
            var result = _classUnderTest.TestValidate(model);
            //Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ProcessName);
        }
    }
}
