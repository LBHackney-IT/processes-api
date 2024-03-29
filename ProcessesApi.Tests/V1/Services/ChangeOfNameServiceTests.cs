using AutoFixture;
using FluentAssertions;
using Moq;
using Hackney.Shared.Processes.Domain;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hackney.Core.Sns;
using Xunit;
using ProcessesApi.V1.Services;
using System.Linq;
using System.Globalization;
using Hackney.Shared.Processes.Domain.Constants.Shared;
using ProcessesApi.V1.Services.Exceptions;
using ProcessesApi.V1.Helpers;
using Hackney.Shared.Person.Domain;
using Hackney.Shared.Processes.Domain.Constants;
using Hackney.Shared.Processes.Domain.Constants.ChangeOfName;
using ChangeOfNameKeys = Hackney.Shared.Processes.Domain.Constants.ChangeOfName.ChangeOfNameKeys;
using ChangeOfNamePermittedTriggers = Hackney.Shared.Processes.Domain.Constants.ChangeOfName.ChangeOfNamePermittedTriggers;
using SharedKeys = Hackney.Shared.Processes.Domain.Constants.SharedKeys;
using SharedPermittedTriggers = Hackney.Shared.Processes.Domain.Constants.SharedPermittedTriggers;
using Hackney.Shared.Processes.Sns;

namespace ProcessesApi.Tests.V1.Services
{
    [Collection("AppTest collection")]
    public class ChangeOfNameServiceTests : ProcessServiceBaseTests, IDisposable
    {
        private Mock<IDbOperationsHelper> _mockDbOperationsHelper;

        public ChangeOfNameServiceTests(AwsMockWebApplicationFactory<Startup> appFactory) : base(appFactory)
        {
            _mockSnsGateway = new Mock<ISnsGateway>();
            _mockDbOperationsHelper = new Mock<IDbOperationsHelper>();
            _classUnderTest = new ChangeOfNameService(_mockSnsGateway.Object, _mockDbOperationsHelper.Object);

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
            process.PreviousStates.Should().HaveCount(1);

            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
            _lastSnsEvent.EventType.Should().Be(EventConstants.PROCESS_STARTED_AGAINST_PERSON_EVENT);
        }

        [Theory]
        [InlineData(ChangeOfNameStates.EnterNewName, ChangeOfNamePermittedTriggers.EnterNewName, new string[] { ChangeOfNameKeys.FirstName, ChangeOfNameKeys.Surname, ChangeOfNameKeys.Title })]
        [InlineData(SharedStates.DocumentsAppointmentRescheduled, SharedPermittedTriggers.RescheduleDocumentsAppointment, new string[] { SharedKeys.AppointmentDateTime })]
        [InlineData(SharedStates.DocumentsRequestedAppointment, SharedPermittedTriggers.RescheduleDocumentsAppointment, new string[] { SharedKeys.AppointmentDateTime })]
        [InlineData(ChangeOfNameStates.NameSubmitted, SharedPermittedTriggers.RequestDocumentsAppointment, new string[] { SharedKeys.AppointmentDateTime })]
        [InlineData(SharedStates.ApplicationSubmitted, SharedPermittedTriggers.TenureInvestigation, new string[] { SharedKeys.TenureInvestigationRecommendation })]
        [InlineData(SharedStates.TenureInvestigationFailed, SharedPermittedTriggers.HOApproval, new string[] { SharedKeys.HousingAreaManagerName, SharedKeys.HORecommendation })]
        [InlineData(SharedStates.TenureInvestigationPassed, SharedPermittedTriggers.HOApproval, new string[] { SharedKeys.HousingAreaManagerName, SharedKeys.HORecommendation })]
        [InlineData(SharedStates.TenureInvestigationPassedWithInt, SharedPermittedTriggers.HOApproval, new string[] { SharedKeys.HousingAreaManagerName, SharedKeys.HORecommendation })]
        [InlineData(SharedStates.HOApprovalPassed, SharedPermittedTriggers.ScheduleTenureAppointment, new string[] { SharedKeys.AppointmentDateTime })]
        [InlineData(SharedStates.TenureAppointmentScheduled, SharedPermittedTriggers.RescheduleTenureAppointment, new string[] { SharedKeys.AppointmentDateTime })]
        [InlineData(SharedStates.TenureAppointmentRescheduled, SharedPermittedTriggers.RescheduleTenureAppointment, new string[] { SharedKeys.AppointmentDateTime })]

        public async Task ThrowsFormDataNotFoundException(string initialState, string trigger, string[] expectedFormDataKeys)
        {
            await ShouldThrowFormDataNotFoundException(initialState, trigger, expectedFormDataKeys).ConfigureAwait(false);
        }

        #region Close Process
        [Theory]
        [InlineData(SharedStates.DocumentsRequestedDes)]
        [InlineData(SharedStates.DocumentsRequestedAppointment)]
        [InlineData(SharedStates.DocumentsAppointmentRescheduled)]
        [InlineData(SharedStates.TenureAppointmentRescheduled)]

        public async Task ProcessStateIsUpdatedToProcessClosedAndEventIsRaised(string fromState)
        {
            await ProcessStateShouldUpdateToProcessClosedAndEventIsRaised(fromState).ConfigureAwait(false);
        }

        #endregion

        #region Cancel Process

        // List all states that CancelProcess can be triggered from
        [Theory]
        [InlineData(ChangeOfNameStates.NameSubmitted)]
        [InlineData(SharedStates.TenureAppointmentScheduled)]
        [InlineData(SharedStates.TenureAppointmentRescheduled)]
        public async Task ProcessStateIsUpdatedToProcessCancelledAndProcessClosedEventIsRaised(string fromState)
        {
            await ProcessStateShouldUpdateToProcessCancelledAndProcessClosedEventIsRaised(fromState).ConfigureAwait(false);
        }

        # endregion

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
                { ChangeOfNameKeys.Surname, "newSurname"},
                {ChangeOfNameKeys.Title,  Title.Miss}
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
                                                 new List<string>() { SharedPermittedTriggers.RequestDocumentsAppointment, SharedPermittedTriggers.ReviewDocuments, SharedPermittedTriggers.CloseProcess });
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
                    SharedPermittedTriggers.CloseProcess
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
                new List<string> { SharedPermittedTriggers.RescheduleDocumentsAppointment, SharedPermittedTriggers.CloseProcess, SharedPermittedTriggers.ReviewDocuments }
            );
            process.PreviousStates.Last().State.Should().Be(initialState);
            VerifyThatProcessUpdatedEventIsTriggered(initialState, SharedStates.DocumentsAppointmentRescheduled);

            var stateData = (_lastSnsEvent.EventData.NewData as ProcessStateChangeData).StateData;
            stateData.Should().ContainKey(SharedKeys.AppointmentDateTime);
        }

        #endregion

        #region Submit Application

        [Fact]
        public async Task ProcessStateIsUpdatedToApplicationSubmittedOnSubmitApplicationTrigger()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SharedStates.DocumentChecksPassed);
            var trigger = CreateProcessTrigger(process, SharedPermittedTriggers.SubmitApplication);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SharedStates.ApplicationSubmitted,
                new List<string> { SharedPermittedTriggers.TenureInvestigation });

            process.PreviousStates.Last().State.Should().Be(SharedStates.DocumentChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SharedStates.DocumentChecksPassed, SharedStates.ApplicationSubmitted);
        }

        #endregion

        #region Tenure Investigation

        [Theory]
        [InlineData(SharedValues.Approve, SharedStates.TenureInvestigationPassed)]
        [InlineData(SharedValues.Decline, SharedStates.TenureInvestigationFailed)]
        [InlineData(SharedValues.Appointment, SharedStates.TenureInvestigationPassedWithInt)]
        public async Task ProcessStateIsUpdatedOnTenureInvestigationTrigger(string tenureInvestigationRecommendation, string expectedState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SharedStates.ApplicationSubmitted);
            var formData = new Dictionary<string, object>
            {
                {  SharedKeys.TenureInvestigationRecommendation, tenureInvestigationRecommendation }
            };
            var trigger = CreateProcessTrigger(process, SharedPermittedTriggers.TenureInvestigation, formData);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, expectedState,
                new List<string> { SharedPermittedTriggers.HOApproval, SharedPermittedTriggers.ScheduleInterview }
            );
            process.PreviousStates.Last().State.Should().Be(SharedStates.ApplicationSubmitted);
            VerifyThatProcessUpdatedEventIsTriggered(SharedStates.ApplicationSubmitted, expectedState);
        }

        #endregion

        #region Schedule Interview

        [Fact]
        public async Task ProcessStateIsUpdatedToInterviewScheduledOnScheduleInterviewTrigger()
        {
            // Arrange
            var appointmentDateTime = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);
            var process = CreateProcessWithCurrentState(SharedStates.TenureInvestigationPassedWithInt);
            var trigger = CreateProcessTrigger(process, SharedPermittedTriggers.ScheduleInterview, new Dictionary<string, object>
            {
                { SharedKeys.AppointmentDateTime, appointmentDateTime }
            });

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SharedStates.InterviewScheduled,
                new List<string> { SharedPermittedTriggers.RescheduleInterview, SharedPermittedTriggers.HOApproval, SharedPermittedTriggers.CancelProcess });

            process.PreviousStates.Last().State.Should().Be(SharedStates.TenureInvestigationPassedWithInt);
            VerifyThatProcessUpdatedEventIsTriggered(SharedStates.TenureInvestigationPassedWithInt, SharedStates.InterviewScheduled);
        }

        #endregion

        #region Reschedule Interview

        [Theory]
        [InlineData(SharedStates.InterviewScheduled)]
        [InlineData(SharedStates.InterviewRescheduled)]
        public async Task ProcessStateIsUpdatedToInterviewRescheduledOnScheduleInterview(string initialState)
        {
            // Arrange
            var appointmentDateTime = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);
            var process = CreateProcessWithCurrentState(initialState);
            var trigger = CreateProcessTrigger(process, SharedPermittedTriggers.RescheduleInterview, new Dictionary<string, object>
            {
                { SharedKeys.AppointmentDateTime, appointmentDateTime }
            });

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SharedStates.InterviewRescheduled,
                new List<string> { SharedPermittedTriggers.HOApproval, SharedPermittedTriggers.RescheduleInterview, SharedPermittedTriggers.CancelProcess });

            process.PreviousStates.Last().State.Should().Be(initialState);
            VerifyThatProcessUpdatedEventIsTriggered(initialState, SharedStates.InterviewRescheduled);
        }

        #endregion

        #region HOApproval

        [Theory]
        [InlineData(SharedStates.InterviewScheduled)]
        [InlineData(SharedStates.InterviewRescheduled)]
        [InlineData(SharedStates.TenureInvestigationPassedWithInt)]
        [InlineData(SharedStates.TenureInvestigationPassed)]
        [InlineData(SharedStates.TenureInvestigationFailed)]

        public async Task ProcessStateIsUpdatedToHOApprovalPassed(string initialState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(initialState);
            var formData = new Dictionary<string, object>
            {
                {  SharedKeys.HORecommendation, SharedValues.Approve },
                {  SharedKeys.HousingAreaManagerName, "ManagerName"  },
                {  SharedKeys.Reason, "Some Reason"  }
            };
            var trigger = CreateProcessTrigger(process, SharedPermittedTriggers.HOApproval, formData);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SharedStates.HOApprovalPassed,
                new List<string> { SharedPermittedTriggers.ScheduleTenureAppointment, SharedPermittedTriggers.CancelProcess }
            );
            process.PreviousStates.Last().State.Should().Be(initialState);
            VerifyThatProcessUpdatedEventIsTriggered(initialState, SharedStates.HOApprovalPassed);
        }

        [Theory]
        [InlineData(SharedStates.InterviewScheduled)]
        [InlineData(SharedStates.InterviewRescheduled)]
        [InlineData(SharedStates.TenureInvestigationPassedWithInt)]
        [InlineData(SharedStates.TenureInvestigationPassed)]
        [InlineData(SharedStates.TenureInvestigationFailed)]
        public async Task ProcessStateIsUpdatedToHOApprovalFailed(string initialState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(initialState);
            var formData = new Dictionary<string, object>
            {
                {  SharedKeys.HORecommendation, SharedValues.Decline },
                { SharedKeys.HousingAreaManagerName, "ManagerName"},
                { SharedKeys.Reason, "Some Reason"}
            };

            var trigger = CreateProcessTrigger(process, SharedPermittedTriggers.HOApproval, formData);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SharedStates.HOApprovalFailed,
                new List<string> { SharedPermittedTriggers.CloseProcess }
            );
            process.PreviousStates.Last().State.Should().Be(initialState);
            VerifyThatProcessUpdatedEventIsTriggered(initialState, SharedStates.HOApprovalFailed);
        }

        [Theory]
        [InlineData(SharedStates.InterviewScheduled)]
        [InlineData(SharedStates.InterviewRescheduled)]
        [InlineData(SharedStates.TenureInvestigationPassedWithInt)]
        [InlineData(SharedStates.TenureInvestigationPassed)]
        [InlineData(SharedStates.TenureInvestigationFailed)]
        public async Task ThrowsFormDataInvalidExceptionOnHOApprovalWhenRecommendationIsNotOneOfCorrectValues(string initialState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(initialState);
            var invalidRecommendation = "some invalid value";
            var formData = new Dictionary<string, object>
            {
                {  SharedKeys.HORecommendation, invalidRecommendation },
                { SharedKeys.HousingAreaManagerName, "ManagerName"},
                { SharedKeys.Reason, "Some reason"}
            };
            var trigger = CreateProcessTrigger(process, SharedPermittedTriggers.HOApproval, formData);
            var expectedRecommendationValues = new List<string>()
            {
                SharedValues.Approve,
                SharedValues.Decline
            };

            var expectedErrorMessage = String.Format("The request's FormData is invalid: The form data value supplied for key {0} does not match any of the expected values ({1}). The value supplied was: {2}",
                                                    SharedKeys.HORecommendation,
                                                    String.Join(", ", expectedRecommendationValues),
                                                    invalidRecommendation);

            // Act & assert
            (await _classUnderTest
                .Invoking(cut => cut.Process(trigger, process, _token))
                .Should().ThrowAsync<FormDataInvalidException>()).WithMessage(expectedErrorMessage);
        }

        #endregion

        #region Schedule Tenure Appointment
        [Fact]
        public async Task ProcessStateIsUpdatedToScheduleTenureAppointmentOnHOApprovalPassed()
        {
            // Arrange
            var appointmentDateTime = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);
            var process = CreateProcessWithCurrentState(SharedStates.HOApprovalPassed);
            var trigger = CreateProcessTrigger(process, SharedPermittedTriggers.ScheduleTenureAppointment, new Dictionary<string, object>
            {
                { SharedKeys.AppointmentDateTime, appointmentDateTime }
            });

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SharedStates.TenureAppointmentScheduled,
                new List<string> { SharedPermittedTriggers.RescheduleTenureAppointment, ChangeOfNamePermittedTriggers.UpdateName, SharedPermittedTriggers.CancelProcess }
             );

            process.PreviousStates.Last().State.Should().Be(SharedStates.HOApprovalPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SharedStates.HOApprovalPassed, SharedStates.TenureAppointmentScheduled);
        }


        #endregion

        #region Reschedule Tenure Appointment
        [Theory]
        [InlineData(SharedStates.TenureAppointmentScheduled)]
        [InlineData(SharedStates.TenureAppointmentRescheduled)]
        public async Task ProcessStateIsUpdatedToRescheduleTenureAppointmentOnScheduleAppointment(string initialState)
        {
            // Arrange
            var appointmentDateTime = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);
            var process = CreateProcessWithCurrentState(initialState, new Dictionary<string, object>
            {
                { SharedKeys.AppointmentDateTime, appointmentDateTime }
            });
            var trigger = CreateProcessTrigger(process, SharedPermittedTriggers.RescheduleTenureAppointment, new Dictionary<string, object>
            {
                { SharedKeys.AppointmentDateTime, appointmentDateTime }
            });

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SharedStates.TenureAppointmentRescheduled,
                new List<string> { SharedPermittedTriggers.CancelProcess, SharedPermittedTriggers.RescheduleTenureAppointment, ChangeOfNamePermittedTriggers.UpdateName, SharedPermittedTriggers.CloseProcess }
            );

            process.PreviousStates.Last().State.Should().Be(initialState);
            VerifyThatProcessUpdatedEventIsTriggered(initialState, SharedStates.TenureAppointmentRescheduled);
        }

        #endregion

        #region UpdateName

        [Theory]
        [InlineData(SharedStates.TenureAppointmentScheduled)]
        [InlineData(SharedStates.TenureAppointmentRescheduled)]
        public async Task ProcessStateIsUpdatedToProcessCompletedAndEventIsRaisedOnUpdateName(string initialState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(initialState);
            var formData = new Dictionary<string, object>()
            {
                { SharedKeys.HasNotifiedResident, true },
            };
            var random = new Random();
            if (random.Next() % 2 == 0) // randomly add reason to formdata
                formData.Add(SharedKeys.Reason, "this is a reason.");

            var triggerObject = CreateProcessTrigger(process,
                                                     ChangeOfNamePermittedTriggers.UpdateName,
                                                     formData);
            _mockDbOperationsHelper.Setup(x => x.UpdatePerson(process, _token));

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 ChangeOfNameStates.NameUpdated,
                                                 new List<string>());
            process.PreviousStates.LastOrDefault().State.Should().Be(initialState);

            _mockDbOperationsHelper.Verify(x => x.UpdatePerson(process, _token), Times.Once);
            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _lastSnsEvent.EventType.Should().Be(EventConstants.PROCESS_COMPLETED_EVENT);
        }

        [Fact]
        public async Task ThrowsErrorIfDbOperationsHelperThrowsError()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SharedStates.TenureAppointmentScheduled);

            var formData = new Dictionary<string, object> {
                { SharedKeys.HasNotifiedResident, true },
            };

            var triggerObject = CreateProcessTrigger(process,
                                                     ChangeOfNamePermittedTriggers.UpdateName,
                                                     formData);
            _mockDbOperationsHelper.Setup(x => x.UpdatePerson(process, _token)).Throws(new Exception("Test Exception"));

            // Act + Assert
            await _classUnderTest.Invoking(x => x.Process(triggerObject, process, _token))
                           .Should().ThrowAsync<Exception>();
        }

        #endregion

    }
}
