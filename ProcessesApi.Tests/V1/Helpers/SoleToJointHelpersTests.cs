using ProcessesApi.V1.Helpers;
using Xunit;
using FluentAssertions;
using AutoFixture;
using ProcessesApi.V1.Domain;
using System.Collections.Generic;
using ProcessesApi.V1.Constants.Shared;
using ProcessesApi.V1.Constants;
using System;
using ProcessesApi.V1.Services.Exceptions;

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
        public void ValidateManualCheckChoosesFailedTriggerIfFormDataDoesNotMatchExpectedValues()
        {
            // Arrange
            var processRequest = _fixture.Create<ProcessTrigger>();
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
            var processRequest = _fixture.Create<ProcessTrigger>();
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

        [Fact]
        public void ShouldNotThrowErrorIfValidUserInput()
        {
            var processRequest = _fixture.Create<ProcessTrigger>();

            var triggerMappings = new Dictionary<string, string>
            {
                {SharedValues.Appointment, SharedInternalTriggers.TenureInvestigationPassedWithInt },
                { SharedValues.Approve, SharedInternalTriggers.TenureInvestigationPassed },
                { SharedValues.Decline, SharedInternalTriggers.TenureInvestigationFailed }
            };

            Action action = () => processRequest.SelectTriggerFromUserInput(triggerMappings, SharedKeys.TenureInvestigationRecommendation, null);

            action.Should().NotThrow<FormDataValueInvalidException>();
        }


        [Fact]
        public void ShouldThrowFormDataNotFoundError()
        {
            var processRequest = _fixture.Create<ProcessTrigger>();

            var triggerMappings = new Dictionary<string, string>
            {
                {"Approval", SharedInternalTriggers.TenureInvestigationPassed },
                { SharedValues.Appointment, SharedInternalTriggers.TenureInvestigationPassedWithInt },
                { SharedValues.Decline, SharedInternalTriggers.TenureInvestigationFailed }
            };

            Action action = () => processRequest.SelectTriggerFromUserInput(triggerMappings, SharedKeys.TenureInvestigationRecommendation, null);

            action.Should().Throw<FormDataNotFoundException>();
        }

    }
}
