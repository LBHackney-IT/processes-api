using FluentValidation.TestHelper;
using ProcessesApi.V1.Boundary.Request.Validation;
using ProcessesApi.V1.Domain;
using Xunit;
using System;
using System.Collections.Generic;
using ProcessesApi.V1.Domain.SoleToJoint;
using AutoFixture;

namespace ProcessesApi.Tests.V1.Boundary.Validation
{
    public class ProcessValidatorTests
    {
        private readonly ProcessValidator _classUnderTest;
        private readonly Fixture _fixture = new Fixture();


        public ProcessValidatorTests()
        {
            _classUnderTest = new ProcessValidator();
        }

        [Fact]
        public void RequestShouldErrorWithEmptyId()
        {
            //Arrange
            var query = _fixture.Build<SoleToJointProcess>().With(x => x.Id, Guid.Empty).Create();
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.Id);
        }

        [Fact]
        public void RequestShouldErrorWithEmptyTargetId()
        {
            //Arrange
            var query = _fixture.Build<SoleToJointProcess>().With(x => x.TargetId, Guid.Empty).Create();
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldHaveValidationErrorFor(x => x.TargetId);
        }

        [Fact]
        public void RequestShouldErrorWithEmptyRelatedEntity()
        {
            //Arrange
            var query = _fixture.Build<SoleToJointProcess>().With(x => x.RelatedEntities, new List<Guid> { Guid.Empty }).Create();
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
            var query = _fixture.Build<SoleToJointProcess>().With(x => x.ProcessName, processName).Create();
            //Act
            var result = _classUnderTest.TestValidate(query);
            //Assert
            result.ShouldNotHaveValidationErrorFor(x => x.ProcessName);
        }
    }
}
