using AutoFixture;
using FluentAssertions;
using Moq;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Constants.ChangeOfName;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hackney.Core.Sns;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure.JWT;
using Xunit;
using ProcessesApi.V1.Services;
using ProcessesApi.V1.Constants;
using System.Linq;

namespace ProcessesApi.Tests.V1.Services
{
    [Collection("AppTest collection")]
    public class ChangeOfNameServiceTests : ProcessServiceBaseTests, IDisposable
    {

        public ChangeOfNameServiceTests(AwsMockWebApplicationFactory<Startup> appFactory) : base(appFactory)
        {
            _mockSnsGateway = new Mock<ISnsGateway>();
            _classUnderTest = new ChangeOfNameService(new ProcessesSnsFactory(), _mockSnsGateway.Object);

            _mockSnsGateway
                .Setup(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<EntityEventSns, string, string>((ev, s1, s2) => _lastSnsEvent = ev);
        }

        [Fact]
        public async Task InitialiseStateToEnterNewNameIfCurrentStateIsNotDefinedAndTriggerProcessStartedEvents()
        {
            // Arrange
            var process = _fixture.Build<Process>()
                                    .With(x => x.CurrentState, (ProcessState) null)
                                    .With(x => x.PreviousStates, new List<ProcessState>())
                                    .Create();
            var triggerObject = CreateProcessTrigger(process,
                                                     SharedPermittedTriggers.StartApplication,
                                                     _fixture.Create<Dictionary<string, object>>());
            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);
            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 ChangeOfNameStates.EnterNewName,
                                                 new List<string>() { ChangeOfNamePermittedTriggers.EnterNewName });
            process.PreviousStates.Should().BeEmpty();

            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
            _lastSnsEvent.EventType.Should().Be(ProcessEventConstants.PROCESS_STARTED_AGAINST_PERSON_EVENT);
        }

        [Fact]
        public async Task CurrentStateIsUpdatedToNameSubmittedOnEnterNewName()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(ChangeOfNameStates.EnterNewName);

            var newName = "Update";
            var formData = new Dictionary<string, object>
            {
                { ChangeOfNameKeys.FirstName, newName },
            };

            var triggerObject = CreateProcessTrigger(process,
                                                     ChangeOfNamePermittedTriggers.EnterNewName,
                                                     formData);

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 ChangeOfNameStates.NameSubmitted,
                                                 new List<string>() { /*TODO: Add next state here */ });
            process.PreviousStates.LastOrDefault().State.Should().Be(ChangeOfNameStates.EnterNewName);
            VerifyThatProcessUpdatedEventIsTriggered(ChangeOfNameStates.EnterNewName, ChangeOfNameStates.NameSubmitted);
        }

        [Theory]
        [InlineData(ChangeOfNameStates.EnterNewName, ChangeOfNamePermittedTriggers.EnterNewName, new string[] { ChangeOfNameKeys.Title, ChangeOfNameKeys.FirstName, ChangeOfNameKeys.MiddleName, ChangeOfNameKeys.Surname })]

        public void ThrowsFormDataNotFoundException(string initialState, string trigger, string[] expectedFormDataKeys)
        {
            ShouldThrowFormDataNotFoundException(initialState, trigger, expectedFormDataKeys);
        }

    }
}