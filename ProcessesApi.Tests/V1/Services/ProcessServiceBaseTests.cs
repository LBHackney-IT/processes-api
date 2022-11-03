using Moq;
using System;
using System.Collections.Generic;
using Hackney.Core.JWT;
using Hackney.Core.Sns;
using Xunit;
using ProcessesApi.V1.Services.Interfaces;
using Hackney.Shared.Processes.Domain;
using FluentAssertions;
using ProcessesApi.V1.Services.Exceptions;
using System.Threading.Tasks;
using System.Linq;
using AutoFixture;
using Hackney.Shared.Processes.Domain.Constants;
using SharedKeys = Hackney.Shared.Processes.Domain.Constants.SharedKeys;
using SharedPermittedTriggers = Hackney.Shared.Processes.Domain.Constants.SharedPermittedTriggers;
using Hackney.Shared.Processes.Sns;
using ProcessesApi.V1.Services;
using Microsoft.Extensions.Logging;

namespace ProcessesApi.Tests.V1.Services
{
    [Collection("AppTest collection")]
    public class ProcessServiceBaseTests : IDisposable
    {
        public IProcessService _classUnderTest;
        public Fixture _fixture = new Fixture();
        protected readonly List<Action> _cleanup = new List<Action>();

        protected Mock<ISnsGateway> _mockSnsGateway;
        protected readonly Token _token = new Token();
        protected EntityEventSns _lastSnsEvent = new EntityEventSns();
        protected Mock<ILogger<ProcessService>> _mockLogger;

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        protected bool _disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                foreach (var action in _cleanup)
                    action();

                _disposed = true;
            }
        }

        public ProcessServiceBaseTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _mockSnsGateway = new Mock<ISnsGateway>();
            _mockLogger = new Mock<ILogger<ProcessService>>();
            _mockSnsGateway
                .Setup(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<EntityEventSns, string, string>((ev, s1, s2) => _lastSnsEvent = ev);
        }



        public Process CreateProcessWithCurrentState(string currentState, Dictionary<string, object> formData = null)
        {
            return _fixture.Build<Process>()
                          .With(x => x.CurrentState,
                                    _fixture.Build<ProcessState>()
                                           .With(x => x.State, currentState)
                                           .With(x => x.ProcessData, _fixture.Build<ProcessData>()
                                                                            .With(x => x.FormData, formData ?? new Dictionary<string, object>())
                                                                            .Create())
                                           .Create())
                          .Create();
        }

        protected ProcessTrigger CreateProcessTrigger(Process process, string trigger, Dictionary<string, object> formData = null)
        {
            return ProcessTrigger.Create
            (
                process.Id,
                trigger,
                formData,
                new List<Guid>()
            );
        }

        protected void CurrentStateShouldContainCorrectData(Process process, ProcessTrigger triggerObject, string expectedCurrentState, List<string> expectedTriggers)
        {
            process.CurrentState.State.Should().Be(expectedCurrentState);
            process.CurrentState.PermittedTriggers.Should().BeEquivalentTo(expectedTriggers);
            process.CurrentState.ProcessData.FormData.Should().BeEquivalentTo(triggerObject.FormData);
            process.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(triggerObject.Documents);
            process.CurrentState.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMilliseconds(2000));
            process.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMilliseconds(2000));
        }

        protected void VerifyThatProcessUpdatedEventIsTriggered(string oldState, string newState)
        {
            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _lastSnsEvent.EventType.Should().Be(EventConstants.PROCESS_UPDATED_EVENT);
            (_lastSnsEvent.EventData.OldData as ProcessStateChangeData).State.Should().Be(oldState);
            (_lastSnsEvent.EventData.NewData as ProcessStateChangeData).State.Should().Be(newState);
        }

        protected async Task ShouldThrowFormDataNotFoundException(string initialState, string trigger, string[] expectedFormDataKeys)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(initialState);

            var triggerObject = CreateProcessTrigger(process,
                                                     trigger,
                                                     new Dictionary<string, object>());

            var expectedErrorMessage = $"The request's FormData is invalid: The form data keys supplied () do not include the expected values ({String.Join(", ", expectedFormDataKeys)}).";
            // Act + Assert
            (await _classUnderTest.Invoking(x => x.Process(triggerObject, process, _token))
                           .Should().ThrowAsync<FormDataNotFoundException>()).WithMessage(expectedErrorMessage);
        }

        protected async Task ProcessStateShouldUpdateToProcessClosedAndEventIsRaised(string fromState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(fromState);
            var formData = new Dictionary<string, object>()
            {
                { SharedKeys.HasNotifiedResident, true }
            };

            var random = new Random();
            if (random.Next() % 2 == 0) // randomly add reason to formdata
                formData.Add(SharedKeys.Reason, "this is a reason.");

            var triggerObject = CreateProcessTrigger(process,
                                                     SharedPermittedTriggers.CloseProcess,
                                                     formData);

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SharedStates.ProcessClosed,
                                                 new List<string>());
            process.PreviousStates.LastOrDefault().State.Should().Be(fromState);

            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _lastSnsEvent.EventType.Should().Be(EventConstants.PROCESS_CLOSED_EVENT);
        }


        protected async Task ProcessStateShouldUpdateToProcessCancelledAndProcessClosedEventIsRaised(string fromState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(fromState);
            var formData = new Dictionary<string, object>()
            {
                { SharedKeys.Comment, "Some comment" }
            };

            var triggerObject = CreateProcessTrigger(process,
                                                     SharedPermittedTriggers.CancelProcess,
                                                     formData);

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SharedStates.ProcessCancelled,
                                                 new List<string>());
            process.PreviousStates.LastOrDefault().State.Should().Be(fromState);

            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _lastSnsEvent.EventType.Should().Be(EventConstants.PROCESS_CLOSED_EVENT);
        }
    }
}
