using AutoFixture;
using FluentAssertions;
using Moq;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Constants.SoleToJoint;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hackney.Core.Sns;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure.JWT;
using Xunit;
using ProcessesApi.V1.Services;
using ProcessesApi.V1.Helpers;
using ProcessesApi.V1.Services.Exceptions;
using System.Globalization;
using ProcessesApi.V1.Constants;
using ProcessesApi.V1.Constants.Shared;

namespace ProcessesApi.Tests.V1.Services
{
    [Collection("AppTest collection")]
    public class SoleToJointServiceTests : ProcessServiceBaseTests, IDisposable
    {
        private Mock<IDbOperationsHelper> _mockDbOperationsHelper;

        private Dictionary<string, object> _manualEligibilityPassData => new Dictionary<string, object>
        {
            { SoleToJointKeys.BR11, "true" },
            { SoleToJointKeys.BR12, "false" },
            { SoleToJointKeys.BR13, "false" },
            { SoleToJointKeys.BR15, "false" },
            { SoleToJointKeys.BR16, "false" },
            { SoleToJointKeys.BR7, "false"},
            { SoleToJointKeys.BR8, "false" },
            { SoleToJointKeys.BR9, "false" }
        };

        private readonly Dictionary<string, object> _tenancyBreachPassData = new Dictionary<string, object>
        {
            { SoleToJointKeys.BR5, "true" },
            { SoleToJointKeys.BR10, "true" },
            { SoleToJointKeys.BR17, "false" },
            { SoleToJointKeys.BR18, "false" }
        };

        public SoleToJointServiceTests(AwsMockWebApplicationFactory<Startup> appFactory) : base(appFactory)
        {
            _mockSnsGateway = new Mock<ISnsGateway>();
            _mockDbOperationsHelper = new Mock<IDbOperationsHelper>();

            _classUnderTest = new SoleToJointService(new ProcessesSnsFactory(), _mockSnsGateway.Object, _mockDbOperationsHelper.Object);

            _mockSnsGateway
                .Setup(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<EntityEventSns, string, string>((ev, s1, s2) => _lastSnsEvent = ev);
        }

        // List states & triggers that expect certain form data values
        [Theory]
        [InlineData(SoleToJointStates.AutomatedChecksFailed, SharedPermittedTriggers.CloseProcess, new string[] { SharedKeys.HasNotifiedResident })]
        [InlineData(SharedStates.DocumentsRequestedDes, SharedPermittedTriggers.CloseProcess, new string[] { SharedKeys.HasNotifiedResident })]
        [InlineData(SoleToJointStates.TenureUpdated, SharedPermittedTriggers.CompleteProcess, new string[] { SharedKeys.HasNotifiedResident })]

        [InlineData(SoleToJointStates.SelectTenants, SoleToJointPermittedTriggers.CheckAutomatedEligibility, new string[] { SoleToJointKeys.IncomingTenantId, SoleToJointKeys.TenantId })]
        [InlineData(SoleToJointStates.AutomatedChecksPassed, SoleToJointPermittedTriggers.CheckManualEligibility, new string[] { SoleToJointKeys.BR11, SoleToJointKeys.BR12, SoleToJointKeys.BR13,
                                                                                                                                SoleToJointKeys.BR15, SoleToJointKeys.BR16, SoleToJointKeys.BR7, SoleToJointKeys.BR8, SoleToJointKeys.BR9 })]
        [InlineData(SoleToJointStates.ManualChecksPassed, SoleToJointPermittedTriggers.CheckTenancyBreach, new string[] { SoleToJointKeys.BR5, SoleToJointKeys.BR10, SoleToJointKeys.BR17, SoleToJointKeys.BR18 })]
        [InlineData(SoleToJointStates.BreachChecksPassed, SharedPermittedTriggers.RequestDocumentsAppointment, new string[] { SharedKeys.AppointmentDateTime })]
        [InlineData(SharedStates.ApplicationSubmitted, SharedPermittedTriggers.TenureInvestigation, new string[] { SharedKeys.TenureInvestigationRecommendation })]
        [InlineData(SharedStates.InterviewScheduled, SharedPermittedTriggers.HOApproval, new string[] { SharedKeys.HousingAreaManagerName, SharedKeys.HORecommendation })]
        [InlineData(SharedStates.InterviewRescheduled, SharedPermittedTriggers.HOApproval, new string[] { SharedKeys.HousingAreaManagerName, SharedKeys.HORecommendation })]
        [InlineData(SharedStates.TenureInvestigationFailed, SharedPermittedTriggers.HOApproval, new string[] { SharedKeys.HousingAreaManagerName, SharedKeys.HORecommendation })]
        [InlineData(SharedStates.TenureInvestigationPassed, SharedPermittedTriggers.HOApproval, new string[] { SharedKeys.HousingAreaManagerName, SharedKeys.HORecommendation })]
        [InlineData(SharedStates.TenureInvestigationPassedWithInt, SharedPermittedTriggers.HOApproval, new string[] { SharedKeys.HousingAreaManagerName, SharedKeys.HORecommendation })]
        [InlineData(SharedStates.HOApprovalPassed, SharedPermittedTriggers.ScheduleTenureAppointment, new string[] { SharedKeys.AppointmentDateTime })]
        public void ThrowsFormDataNotFoundException(string initialState, string trigger, string[] expectedFormDataKeys)
        {
            ShouldThrowFormDataNotFoundException(initialState, trigger, expectedFormDataKeys);
        }

        #region Close or Cancel Process

        // List all states that CloseProcess can be triggered from
        [Theory]
        [InlineData(SoleToJointStates.AutomatedChecksFailed)]
        [InlineData(SoleToJointStates.BreachChecksFailed)]
        [InlineData(SharedStates.DocumentsRequestedDes)]
        [InlineData(SharedStates.DocumentsRequestedAppointment)]
        [InlineData(SharedStates.DocumentsAppointmentRescheduled)]
        [InlineData(SharedStates.HOApprovalFailed)]
        [InlineData(SharedStates.TenureAppointmentRescheduled)]
        [InlineData(SoleToJointStates.ManualChecksFailed)]

        public async Task ProcessStateIsUpdatedToProcessClosedAndEventIsRaised(string fromState)
        {
            await ProcessStateShouldUpdateToProcessClosedAndEventIsRaised(fromState).ConfigureAwait(false);
        }

        // List all states that CancelProcess can be triggered from
        [Theory]
        [InlineData(SharedStates.HOApprovalPassed)]
        [InlineData(SharedStates.InterviewScheduled)]
        [InlineData(SharedStates.InterviewRescheduled)]
        [InlineData(SharedStates.TenureAppointmentScheduled)]
        [InlineData(SharedStates.TenureAppointmentRescheduled)]
        [InlineData(SoleToJointStates.AutomatedChecksPassed)]
        [InlineData(SoleToJointStates.ManualChecksPassed)]
        [InlineData(SoleToJointStates.BreachChecksPassed)]
        [InlineData(SharedStates.TenureInvestigationPassed)]
        public async Task ProcessStateIsUpdatedToProcessCancelledAndProcessClosedEventIsRaised(string fromState)
        {
            await ProcessStateShouldUpdateToProcessCancelledAndProcessClosedEventIsRaised(fromState).ConfigureAwait(false);
        }

        # endregion

        [Fact]
        public async Task InitialiseStateToSelectTenantsIfCurrentStateIsNotDefinedAndTriggerProcessStartedEvents()
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
                                                 SoleToJointStates.SelectTenants,
                                                 new List<string>() { SoleToJointPermittedTriggers.CheckAutomatedEligibility });
            process.PreviousStates.Should().BeEmpty();

            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(2));
            _lastSnsEvent.EventType.Should().Be(ProcessEventConstants.PROCESS_STARTED_AGAINST_TENURE_EVENT);
        }

        #region Automated eligibility checks

        [Fact]
        public async Task AddIncomingTenantToRelatedEntitiesOnCheckAutomatedEligibilityTrigger()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.SelectTenants);

            var incomingTenantId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CheckAutomatedEligibility,
                                                     new Dictionary<string, object>
                                                    {
                                                        { SoleToJointKeys.IncomingTenantId, incomingTenantId },
                                                        { SoleToJointKeys.TenantId, tenantId },
                                                    });

            _mockDbOperationsHelper.Setup(x => x.CheckAutomatedEligibility(process.TargetId, incomingTenantId, tenantId)).ReturnsAsync(true);
            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            _mockDbOperationsHelper.Verify(x => x.AddIncomingTenantToRelatedEntities(triggerObject.FormData, process), Times.Once);
        }

        [Fact]
        public async Task CurrentStateIsUpdatedToAutomatedChecksFailedWhenCheckAutomatedEligibilityReturnsFalse()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.SelectTenants);

            var incomingTenantId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var formData = new Dictionary<string, object>
            {
                { SoleToJointKeys.IncomingTenantId, incomingTenantId },
                { SoleToJointKeys.TenantId, tenantId },
            };

            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CheckAutomatedEligibility,
                                                     formData);

            _mockDbOperationsHelper.Setup(x => x.CheckAutomatedEligibility(process.TargetId, incomingTenantId, tenantId)).ReturnsAsync(false);
            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SoleToJointStates.AutomatedChecksFailed,
                                                 new List<string>() { SharedPermittedTriggers.CloseProcess });
            process.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.SelectTenants);

            _mockDbOperationsHelper.Verify(x => x.CheckAutomatedEligibility(process.TargetId, incomingTenantId, tenantId), Times.Once());
            _mockDbOperationsHelper.Verify(x => x.AddIncomingTenantToRelatedEntities(triggerObject.FormData, process), Times.Once);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.SelectTenants, SoleToJointStates.AutomatedChecksFailed);
        }

        [Fact]
        public async Task ProcessStateIsUpdatedToAutomatedChecksPassedWhenCheckAutomatedEligibilityReturnsTrue()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.SelectTenants);

            var incomingTenantId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();
            var formData = new Dictionary<string, object>
            {
                { SoleToJointKeys.IncomingTenantId, incomingTenantId },
                { SoleToJointKeys.TenantId, tenantId },
            };

            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CheckAutomatedEligibility,
                                                     formData);

            _mockDbOperationsHelper.Setup(x => x.CheckAutomatedEligibility(process.TargetId, incomingTenantId, tenantId)).ReturnsAsync(true);

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SoleToJointStates.AutomatedChecksPassed,
                                                 new List<string>() { SoleToJointPermittedTriggers.CheckManualEligibility, SharedPermittedTriggers.CancelProcess });
            process.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.SelectTenants);

            _mockDbOperationsHelper.Verify(x => x.CheckAutomatedEligibility(process.TargetId, incomingTenantId, tenantId), Times.Once());
            _mockDbOperationsHelper.Verify(x => x.AddIncomingTenantToRelatedEntities(triggerObject.FormData, process), Times.Once);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.SelectTenants, SoleToJointStates.AutomatedChecksPassed);
        }

        #endregion

        #region Manual eligibility checks

        [Fact]
        public async Task ProcessStateIsUpdatedToManualChecksPassed()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.AutomatedChecksPassed);

            var formData = _manualEligibilityPassData;
            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CheckManualEligibility,
                                                     formData);
            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SoleToJointStates.ManualChecksPassed,
                                                 new List<string> { SoleToJointPermittedTriggers.CheckTenancyBreach, SharedPermittedTriggers.CancelProcess });
            process.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.AutomatedChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.AutomatedChecksPassed, SoleToJointStates.ManualChecksPassed);
        }

        [Theory]
        [InlineData(SoleToJointKeys.BR11, "false")]
        [InlineData(SoleToJointKeys.BR12, "true")]
        [InlineData(SoleToJointKeys.BR13, "true")]
        [InlineData(SoleToJointKeys.BR15, "true")]
        [InlineData(SoleToJointKeys.BR16, "true")]
        [InlineData(SoleToJointKeys.BR9, "true")]
        public async Task ProcessStateIsUpdatedToManualChecksFailed(string eligibilityCheckId, string value)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.AutomatedChecksPassed);

            var eligibiltyFormData = _manualEligibilityPassData;
            eligibiltyFormData[eligibilityCheckId] = value;

            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CheckManualEligibility,
                                                     eligibiltyFormData);
            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SoleToJointStates.ManualChecksFailed,
                                                 new List<string>() { SharedPermittedTriggers.CloseProcess });
            process.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.AutomatedChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.AutomatedChecksPassed, SoleToJointStates.ManualChecksFailed);
        }

        #endregion

        #region Tenancy breach checks

        [Fact]
        public async Task ProcessStateIsUpdatedToBreachChecksPassedWhenBreachCheckSucceed()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.ManualChecksPassed);
            var trigger = CreateProcessTrigger(
                process, SoleToJointPermittedTriggers.CheckTenancyBreach, _tenancyBreachPassData);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SoleToJointStates.BreachChecksPassed,
                new List<string> { SharedPermittedTriggers.RequestDocumentsDes, SharedPermittedTriggers.RequestDocumentsAppointment, SharedPermittedTriggers.CancelProcess });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.ManualChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.ManualChecksPassed, SoleToJointStates.BreachChecksPassed);
        }

        [Fact]
        public async Task ProcessStateIsUpdatedToBreachChecksPassedWhenBreachCheckSucceeds()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.ManualChecksPassed);
            _tenancyBreachPassData[SoleToJointKeys.BR5] = "false";
            _tenancyBreachPassData[SoleToJointKeys.BR10] = "false";
            var trigger = CreateProcessTrigger(
                process, SoleToJointPermittedTriggers.CheckTenancyBreach, _tenancyBreachPassData);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SoleToJointStates.BreachChecksPassed,
                new List<string> { SharedPermittedTriggers.RequestDocumentsDes, SharedPermittedTriggers.RequestDocumentsAppointment, SharedPermittedTriggers.CancelProcess });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.ManualChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.ManualChecksPassed, SoleToJointStates.BreachChecksPassed);
        }

        [Theory]
        [InlineData(SoleToJointKeys.BR5, "false")]
        [InlineData(SoleToJointKeys.BR10, "false")]
        [InlineData(SoleToJointKeys.BR17, "true")]
        [InlineData(SoleToJointKeys.BR18, "true")]
        public async Task ProcessStateIsUpdatedToBreachChecksFailedWhenBreachCheckFail(string checkId, string value)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.ManualChecksPassed);

            _tenancyBreachPassData[checkId] = value;

            var trigger = CreateProcessTrigger(
                process, SoleToJointPermittedTriggers.CheckTenancyBreach, _tenancyBreachPassData);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SoleToJointStates.BreachChecksFailed,
                new List<string> { SharedPermittedTriggers.CloseProcess });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.ManualChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.ManualChecksPassed, SoleToJointStates.BreachChecksFailed);
        }

        #endregion

        #region Request Documents

        [Fact]
        public async Task ProcessStateIsUpdatedToDocumentsRequestedAppointmentWhenAppointmentDateTimeIsCorrectFormat()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.BreachChecksPassed);
            var formData = new Dictionary<string, object>() { { SharedKeys.AppointmentDateTime, _fixture.Create<DateTime>() } };
            var trigger = CreateProcessTrigger(process, SharedPermittedTriggers.RequestDocumentsAppointment, formData);

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

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.BreachChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.BreachChecksPassed, SharedStates.DocumentsRequestedAppointment);
        }

        #endregion

        #region Request documents via DES

        [Fact]
        public async Task ProcessStateIsUpdatedToDocumentsRequestedDesOnRequestDocumentsDesTrigger()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.BreachChecksPassed);
            var trigger = CreateProcessTrigger(process, SharedPermittedTriggers.RequestDocumentsDes);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SharedStates.DocumentsRequestedDes,
                new List<string>
                {
                    SharedPermittedTriggers.RequestDocumentsAppointment,
                    SharedPermittedTriggers.ReviewDocuments,
                    SharedPermittedTriggers.CloseProcess
                });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.BreachChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.BreachChecksPassed, SharedStates.DocumentsRequestedDes);
        }

        #endregion

        #region Request documents via appointment

        [Fact]
        public async Task ProcessStateIsUpdatedToDocumentsRequestedAppointmentOnRequestDocumentsAppointmentTrigger()
        {
            // Arrange
            var appointmentDateTime = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);

            var process = CreateProcessWithCurrentState(SoleToJointStates.BreachChecksPassed);
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

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.BreachChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.BreachChecksPassed, SharedStates.DocumentsRequestedAppointment);

            var stateData = (_lastSnsEvent.EventData.NewData as ProcessStateChangeData).StateData;
            stateData.Should().ContainKey(SharedKeys.AppointmentDateTime);
        }

        #endregion

        #region Reschedule documents appointment

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
                new List<string>
                {
                    SharedPermittedTriggers.ReviewDocuments,
                    SharedPermittedTriggers.RescheduleDocumentsAppointment,
                    SharedPermittedTriggers.CloseProcess
                }
            );
            process.PreviousStates.Last().State.Should().Be(initialState);
            VerifyThatProcessUpdatedEventIsTriggered(initialState, SharedStates.DocumentsAppointmentRescheduled);

            var stateData = (_lastSnsEvent.EventData.NewData as ProcessStateChangeData).StateData;
            stateData.Should().ContainKey(SharedKeys.AppointmentDateTime);
        }

        #endregion

        #region Tenure Investigation

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

        [Theory]
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
                new List<string> { SharedPermittedTriggers.ScheduleInterview, SharedPermittedTriggers.HOApproval }
            );
            process.PreviousStates.Last().State.Should().Be(SharedStates.ApplicationSubmitted);
            VerifyThatProcessUpdatedEventIsTriggered(SharedStates.ApplicationSubmitted, expectedState);
        }

        [Fact]
        public async Task ProcessStateIsUpdatedToTenureInvestigationPassed()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SharedStates.ApplicationSubmitted);
            var formData = new Dictionary<string, object>
            {
                {  SharedKeys.TenureInvestigationRecommendation, SharedValues.Approve }
            };
            var trigger = CreateProcessTrigger(process, SharedPermittedTriggers.TenureInvestigation, formData);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SharedStates.TenureInvestigationPassed,
                new List<string> { SharedPermittedTriggers.ScheduleInterview, SharedPermittedTriggers.HOApproval, SharedPermittedTriggers.CancelProcess }
            );
            process.PreviousStates.Last().State.Should().Be(SharedStates.ApplicationSubmitted);
            VerifyThatProcessUpdatedEventIsTriggered(SharedStates.ApplicationSubmitted, SharedStates.TenureInvestigationPassed);
        }

        [Fact]
        public void ThrowsFormDataInvalidExceptionOnTenureInvestigationWhenRecommendationIsNotOneOfCorrectValues()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SharedStates.ApplicationSubmitted);
            var invalidRecommendation = "some invalid value";
            var formData = new Dictionary<string, object>
            {
                {  SharedKeys.TenureInvestigationRecommendation, invalidRecommendation }
            };
            var trigger = CreateProcessTrigger(process, SharedPermittedTriggers.TenureInvestigation, formData);
            var expectedRecommendationValues = new List<string>()
            {
                SharedValues.Appointment,
                SharedValues.Approve,
                SharedValues.Decline
            };
            var expectedErrorMessage = String.Format("The request's FormData is invalid: The form data value supplied for key {0} does not match any of the expected values ({1}). The value supplied was: {2}",
                                                    SharedKeys.TenureInvestigationRecommendation,
                                                    String.Join(", ", expectedRecommendationValues),
                                                    invalidRecommendation);

            // Act & assert
            _classUnderTest
                .Invoking(cut => cut.Process(trigger, process, _token))
                .Should().Throw<FormDataValueInvalidException>().WithMessage(expectedErrorMessage);
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
        [InlineData(SharedStates.TenureInvestigationPassedWithInt)]
        [InlineData(SharedStates.TenureInvestigationPassed)]
        [InlineData(SharedStates.TenureInvestigationFailed)]
        [InlineData(SharedStates.InterviewScheduled)]
        [InlineData(SharedStates.InterviewRescheduled)]
        public void ThrowsFormDataInvalidExceptionOnHOApprovalWhenRecommendationIsNotOneOfCorrectValues(string initialState)
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
            _classUnderTest
                .Invoking(cut => cut.Process(trigger, process, _token))
                .Should().Throw<FormDataInvalidException>().WithMessage(expectedErrorMessage);
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
                new List<string> { SharedPermittedTriggers.RescheduleTenureAppointment, SoleToJointPermittedTriggers.UpdateTenure, SharedPermittedTriggers.CancelProcess }
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
                new List<string> { SoleToJointPermittedTriggers.UpdateTenure, SharedPermittedTriggers.CancelProcess, SharedPermittedTriggers.RescheduleTenureAppointment, SharedPermittedTriggers.CloseProcess }
            );

            process.PreviousStates.Last().State.Should().Be(initialState);
            VerifyThatProcessUpdatedEventIsTriggered(initialState, SharedStates.TenureAppointmentRescheduled);
        }

        #endregion

        #region Update Tenure

        [Theory]
        [InlineData(SharedStates.TenureAppointmentScheduled)]
        [InlineData(SharedStates.TenureAppointmentRescheduled)]
        public async Task ProcessStateIsUpdatedToTenureUpdatedAndEventIsRaisedOnUpdateTenure(string initialState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(initialState);
            var formData = new Dictionary<string, object> { { SoleToJointKeys.TenureStartDate, _fixture.Create<DateTime>() } };
            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.UpdateTenure,
                                                     formData);
            var newTenureId = Guid.NewGuid();
            _mockDbOperationsHelper.Setup(x => x.UpdateTenures(process, _token, formData)).ReturnsAsync(newTenureId);

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SoleToJointStates.TenureUpdated,
                                                 new List<string> { SharedPermittedTriggers.CompleteProcess });
            process.PreviousStates.LastOrDefault().State.Should().Be(initialState);

            process.RelatedEntities.Should().Contain(x => x.Id == newTenureId
                                                          && x.TargetType == TargetType.tenure
                                                          && x.SubType == SubType.newTenure);

            _mockDbOperationsHelper.Verify(x => x.UpdateTenures(process, _token, formData), Times.Once);
            VerifyThatProcessUpdatedEventIsTriggered(initialState, SoleToJointStates.TenureUpdated);
        }


        [Fact]
        public void ThrowsErrorIfDbOperationsHelperThrowsErrorInTenureUpdatedStep()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SharedStates.TenureAppointmentScheduled);
            var formData = new Dictionary<string, object> { { SoleToJointKeys.TenureStartDate, _fixture.Create<DateTime>() } };

            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.UpdateTenure,
                                                     formData);
            _mockDbOperationsHelper.Setup(x => x.UpdateTenures(process, _token, formData)).Throws(new Exception("Test Exception"));

            // Act + Assert
            _classUnderTest.Invoking(x => x.Process(triggerObject, process, _token))
                           .Should().Throw<Exception>();
        }

        [Fact]
        public async Task ProcessStateIsUpdatedToProcessCompleted()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.TenureUpdated);
            var formData = new Dictionary<string, object> { { SharedKeys.HasNotifiedResident, true } };

            var triggerObject = CreateProcessTrigger(process,
                                                     SharedPermittedTriggers.CompleteProcess,
                                                     formData);
            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SharedStates.ProcessCompleted,
                                                 new List<string>());
            process.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.TenureUpdated);

            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _lastSnsEvent.EventType.Should().Be(ProcessEventConstants.PROCESS_COMPLETED_EVENT);
        }

        #endregion
    }
}
