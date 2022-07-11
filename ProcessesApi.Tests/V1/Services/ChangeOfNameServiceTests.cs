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
using System.Globalization;

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

        [Theory]
        [InlineData(ChangeOfNameStates.EnterNewName, ChangeOfNamePermittedTriggers.EnterNewName, new string[] { ChangeOfNameKeys.Title, ChangeOfNameKeys.FirstName, ChangeOfNameKeys.MiddleName, ChangeOfNameKeys.Surname })]

        public void ThrowsFormDataNotFoundException(string initialState, string trigger, string[] expectedFormDataKeys)
        {
            ShouldThrowFormDataNotFoundException(initialState, trigger, expectedFormDataKeys);
        }

        #region NameSubmitted
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
                                                 new List<string>() { SharedPermittedTriggers.RequestDocumentsDes, SharedPermittedTriggers.RequestDocumentsAppointment, SharedPermittedTriggers.CancelProcess });
            process.PreviousStates.LastOrDefault().State.Should().Be(ChangeOfNameStates.EnterNewName);
            VerifyThatProcessUpdatedEventIsTriggered(ChangeOfNameStates.EnterNewName, ChangeOfNameStates.NameSubmitted);
        }
        #endregion


        #region RequestDocumentsDes
        [Fact]
        public async Task CurrentStateIsUpdatedToRequestDocumentsDes()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(ChangeOfNameStates.NameSubmitted);

            var triggerObject = CreateProcessTrigger(process,
                                                     SharedPermittedTriggers.RequestDocumentsDes);
            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SharedStates.DocumentsRequestedDes,
                                                 new List<string>() { SharedPermittedTriggers.RequestDocumentsAppointment, SharedPermittedTriggers.ReviewDocuments, SharedPermittedTriggers.CancelProcess });
            process.PreviousStates.LastOrDefault().State.Should().Be(ChangeOfNameStates.NameSubmitted);
            VerifyThatProcessUpdatedEventIsTriggered(ChangeOfNameStates.NameSubmitted, SharedStates.DocumentsRequestedDes);
        }
        #endregion

        #region RequestDocumentsAppointment

        [Theory]
        [InlineData(ChangeOfNameStates.NameSubmitted)]
        [InlineData(SharedStates.DocumentsRequestedDes)]
        public async Task ProcessStateIsUpdatedToDocumentsRequestedAppointment(string initialState)
        {
            // Arrange
            var appointmentDateTime = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);

            var process = CreateProcessWithCurrentState(initialState);
            var trigger = CreateProcessTrigger(process, SharedPermittedTriggers.RequestDocumentsAppointment, new Dictionary<string, object>
            {
                { SharedKeys.AppointmentDateTime, appointmentDateTime }
            });

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SharedStates.DocumentsRequestedAppointment,
                new List<string>
                {
                    SharedPermittedTriggers.RescheduleDocumentsAppointment,
                    SharedPermittedTriggers.ReviewDocuments,
                    SharedPermittedTriggers.CancelProcess
                });

            process.PreviousStates.Last().State.Should().Be(initialState);
            VerifyThatProcessUpdatedEventIsTriggered(initialState, SharedStates.DocumentsRequestedAppointment);

            var stateData = (_lastSnsEvent.EventData.NewData as ProcessStateChangeData).StateData;
            stateData.Should().ContainKey(SharedKeys.AppointmentDateTime);
        }
        #endregion

        #region Reschedule Documents Appointment
        [Theory]
        [InlineData(SharedStates.DocumentsRequestedAppointment)]
        [InlineData(SharedStates.DocumentsAppointmentRescheduled)]
        public async Task ProcessStateIsUpdatedToDocumentsAppointmentRescheduledOnRescheduleDocumentsAppointmentTrigger(string initialState)
        {
            // Arrange
            var appointmentDateTime = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);

            var process = CreateProcessWithCurrentState(initialState, new Dictionary<string, object>
            {
                { SharedKeys.AppointmentDateTime, appointmentDateTime }
            });
            var trigger = CreateProcessTrigger(process, SharedPermittedTriggers.RescheduleDocumentsAppointment, new Dictionary<string, object>
            {
                { SharedKeys.AppointmentDateTime, appointmentDateTime }
            });

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SharedStates.DocumentsAppointmentRescheduled,
                new List<string> { SharedPermittedTriggers.RescheduleDocumentsAppointment /*TODO Add next state here  */}

            );
            process.PreviousStates.Last().State.Should().Be(initialState);
            VerifyThatProcessUpdatedEventIsTriggered(initialState, SharedStates.DocumentsAppointmentRescheduled);

            var stateData = (_lastSnsEvent.EventData.NewData as ProcessStateChangeData).StateData;
            stateData.Should().ContainKey(SharedKeys.AppointmentDateTime);
        }

        #endregion

    }
}
