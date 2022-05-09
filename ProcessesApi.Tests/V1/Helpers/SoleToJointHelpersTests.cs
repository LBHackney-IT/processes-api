using System.Collections.Generic;
using ProcessesApi.V1.Helpers;
using Xunit;
using FluentAssertions;
using System;
using ProcessesApi.V1.UseCase.Exceptions;
using AutoFixture;
using ProcessesApi.V1.Domain;

namespace ProcessesApi.Tests.V1.Helpers
{
    public class SoleToJointHelpersTests
    {
        private Fixture _fixture;

        public SoleToJointHelpersTests()
        {
            _fixture = new Fixture();
        }

        [Fact]
        public void ValidateFormDataThrowsErrorIfFormDataDoesNotContainRequiredValues()
        {
            // Arrange
            var expectedFormDataKey = "some-form-data";
            var requestFormData = new Dictionary<string, object>();
            // Act
            Action action = () => SoleToJointHelpers.ValidateFormData(requestFormData, new List<string>() { expectedFormDataKey });
            // Assert
            action.Should().Throw<FormDataNotFoundException>()
                  .WithMessage($"The form data keys supplied () do not include the expected values ({expectedFormDataKey}).");
        }

        [Fact]
        public void ValidateFormDataDoesNotThrowErrorIfFormDataContainsAllRequiredValues()
        {
            // Arrange
            var expectedFormDataKey = "some-form-data";
            var requestFormData = new Dictionary<string, object>() { { expectedFormDataKey, true } };
            // Act
            Action action = () => SoleToJointHelpers.ValidateFormData(requestFormData, new List<string>() { expectedFormDataKey });
            // Assert
            action.Should().NotThrow<FormDataNotFoundException>();
        }

        [Fact]
        public void ValidateManualCheckChoosesFailedTriggerIfFormDataDoesNotMatchExpectedValues()
        {
            // Arrange
            var processRequest = _fixture.Create<UpdateProcessState>();
            var checkId = "some-check-id";
            var checkSuccessValue = "some-expected-value";
            processRequest.FormData.Add(checkId, "some-other-value");

            var passedTrigger = "pass-trigger";
            var failedTrigger = "fail-trigger";

            // Act
            processRequest.ValidateManualCheck(passedTrigger,
                                               failedTrigger,
                                               (checkId, checkSuccessValue));
            // Assert
            processRequest.Trigger.Should().Be(failedTrigger);
        }

        [Fact]
        public void ValidateManualCheckChoosesPassedTriggerIfFormDataMatchesExpectedValues()
        {
            // Arrange
            var processRequest = _fixture.Create<UpdateProcessState>();
            var checkId = "some-check-id";
            var checkSuccessValue = "some-expected-value";
            processRequest.FormData.Add(checkId, checkSuccessValue);

            var passedTrigger = "pass-trigger";
            var failedTrigger = "fail-trigger";

            // Act
            processRequest.ValidateManualCheck(passedTrigger,
                                               failedTrigger,
                                               (checkId, checkSuccessValue));
            // Assert
            processRequest.Trigger.Should().Be(passedTrigger);
        }
    }
}
