using ProcessesApi.V1.Helpers;
using Xunit;
using FluentAssertions;
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


    }
}
