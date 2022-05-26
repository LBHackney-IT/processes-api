using AutoFixture;
using FluentAssertions;
using Moq;
using ProcessesApi.V1.Domain;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hackney.Core.JWT;
using Hackney.Core.Sns;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure.JWT;
using Xunit;
using ProcessesApi.V1.Services;
using ProcessesApi.V1.Helpers;
using ProcessesApi.V1.Services.Exceptions;
using System.Globalization;

namespace ProcessesApi.Tests.V1.Services
{
    [Collection("AppTest collection")]
    public class SoleToJointServiceTests : IDisposable
    {
        public SoleToJointService _classUnderTest;
        public Fixture _fixture = new Fixture();
        private readonly List<Action> _cleanup = new List<Action>();

        private Mock<ISoleToJointAutomatedEligibilityChecksHelper> _mockAutomatedEligibilityChecksHelper;
        private Mock<ISnsGateway> _mockSnsGateway;
        private readonly Token _token = new Token();
        private EntityEventSns _lastSnsEvent = new EntityEventSns();

        private Dictionary<string, object> _manualEligibilityPassData => new Dictionary<string, object>
        {
            { SoleToJointFormDataKeys.BR11, "true" },
            { SoleToJointFormDataKeys.BR12, "false" },
            { SoleToJointFormDataKeys.BR13, "false" },
            { SoleToJointFormDataKeys.BR15, "false" },
            { SoleToJointFormDataKeys.BR16, "false" },
            { SoleToJointFormDataKeys.BR7, "false"},
            { SoleToJointFormDataKeys.BR8, "false" }
        };

        private readonly Dictionary<string, object> _tenancyBreachPassData = new Dictionary<string, object>
        {
            { SoleToJointFormDataKeys.BR5, "false" },
            { SoleToJointFormDataKeys.BR10, "false" },
            { SoleToJointFormDataKeys.BR17, "false" },
            { SoleToJointFormDataKeys.BR18, "false" }
        };

        private Dictionary<string, object> _reviewDocumentCheckPass => new Dictionary<string, object>
        {
            { SoleToJointFormDataKeys.SeenPhotographicId, "true" },
            { SoleToJointFormDataKeys.SeenSecondId, "true" },
            { SoleToJointFormDataKeys.IsNotInImmigrationControl, "true" },
            {SoleToJointFormDataKeys.SeenProofOfRelationship, "true" },
            { SoleToJointFormDataKeys.IncomingTenantLivingInProperty, "true" }
        };


        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool _disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                foreach (var action in _cleanup)
                    action();

                _disposed = true;
            }
        }

        public SoleToJointServiceTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _mockSnsGateway = new Mock<ISnsGateway>();
            _mockAutomatedEligibilityChecksHelper = new Mock<ISoleToJointAutomatedEligibilityChecksHelper>();

            _classUnderTest = new SoleToJointService(new ProcessesSnsFactory(), _mockSnsGateway.Object, _mockAutomatedEligibilityChecksHelper.Object);

            _mockSnsGateway
                .Setup(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<EntityEventSns, string, string>((ev, s1, s2) => _lastSnsEvent = ev);
        }

        private Process CreateProcessWithCurrentState(string currentState, Dictionary<string, object> formData = null)
        {
            return _fixture.Build<Process>()
                            .With(x => x.CurrentState,
                                    _fixture.Build<ProcessState>()
                                        .With(x => x.State, currentState)
                                        .With(x => x.ProcessData,
                                            _fixture.Build<ProcessData>()
                                                .With(x => x.FormData, formData ?? new Dictionary<string, object>())
                                                .Create())
                                        .Create())
                            .Create();
        }

        private ProcessTrigger CreateProcessTrigger(Process process, string trigger, Dictionary<string, object> formData = null)
        {
            return ProcessTrigger.Create
            (
                process.Id,
                trigger,
                formData,
                _fixture.Create<List<Guid>>()
            );
        }

        private void CurrentStateShouldContainCorrectData(Process process, ProcessTrigger triggerObject, string expectedCurrentState, List<string> expectedTriggers)
        {
            process.CurrentState.State.Should().Be(expectedCurrentState);
            process.CurrentState.PermittedTriggers.Should().BeEquivalentTo(expectedTriggers);
            process.CurrentState.ProcessData.FormData.Should().BeEquivalentTo(triggerObject.FormData);
            process.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(triggerObject.Documents);
            process.CurrentState.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            process.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
        }

        private void VerifyThatProcessUpdatedEventIsTriggered(string oldState, string newState)
        {
            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _lastSnsEvent.EventType.Should().Be(ProcessUpdatedEventConstants.EVENTTYPE);
            (_lastSnsEvent.EventData.OldData as ProcessStateChangeData).State.Should().Be(oldState);
            (_lastSnsEvent.EventData.NewData as ProcessStateChangeData).State.Should().Be(newState);
        }

        // List all states that CloseProcess can be triggered from
        [Theory]
        [InlineData(SoleToJointStates.AutomatedChecksFailed)]
        [InlineData(SoleToJointStates.ManualChecksFailed)]
        [InlineData(SoleToJointStates.BreachChecksFailed)]
        public async Task ProcessStateIsUpdatedToProcessClosedAndProcessClosedEventIsRaised(string fromState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(fromState);
            var formData = new Dictionary<string, object>()
            {
                { SoleToJointFormDataKeys.HasNotifiedResident, true }
            };

            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CloseProcess,
                                                     formData);

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SharedProcessStates.ProcessClosed,
                                                 new List<string>());
            process.PreviousStates.LastOrDefault().State.Should().Be(fromState);

            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _lastSnsEvent.EventType.Should().Be(ProcessClosedEventConstants.EVENTTYPE);
        }

        [Theory]
        [InlineData(SoleToJointStates.AutomatedChecksFailed)]
        [InlineData(SoleToJointStates.ManualChecksFailed)]
        [InlineData(SoleToJointStates.BreachChecksFailed)]
        public async Task ProcessStateIsUpdatedToProcessClosedWithReasonAndProcessClosedEventIsRaised(string fromState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(fromState);
            var formData = new Dictionary<string, object>()
            {
                { SoleToJointFormDataKeys.HasNotifiedResident, true },
                {SoleToJointFormDataKeys.Reason, "This is a reason"}
            };

            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CloseProcess,
                                                     formData);

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SharedProcessStates.ProcessClosed,
                                                 new List<string>());
            process.PreviousStates.LastOrDefault().State.Should().Be(fromState);

            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _lastSnsEvent.EventType.Should().Be(ProcessClosedEventConstants.EVENTTYPE);
        }


        [Fact]
        public async Task InitialiseStateToSelectTenantsIfCurrentStateIsNotDefinedAndTriggerProcessStartedEvent()
        {
            // Arrange
            var process = _fixture.Build<Process>()
                                    .With(x => x.CurrentState, (ProcessState) null)
                                    .With(x => x.PreviousStates, new List<ProcessState>())
                                    .Create();
            var triggerObject = CreateProcessTrigger(process,
                                                     SharedInternalTriggers.StartApplication,
                                                     _fixture.Create<Dictionary<string, object>>());
            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);
            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SoleToJointStates.SelectTenants,
                                                 new List<string>() { SoleToJointPermittedTriggers.CheckAutomatedEligibility });
            process.PreviousStates.Should().BeEmpty();

            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _lastSnsEvent.EventType.Should().Be(ProcessStartedEventConstants.EVENTTYPE);
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
                                                        { SoleToJointFormDataKeys.IncomingTenantId, incomingTenantId },
                                                        { SoleToJointFormDataKeys.TenantId, tenantId },
                                                    });

            _mockAutomatedEligibilityChecksHelper.Setup(x => x.CheckAutomatedEligibility(process.TargetId, incomingTenantId, tenantId)).ReturnsAsync(true);
            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            process.RelatedEntities.Should().Contain(incomingTenantId);
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
                { SoleToJointFormDataKeys.IncomingTenantId, incomingTenantId },
                { SoleToJointFormDataKeys.TenantId, tenantId },
            };

            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CheckAutomatedEligibility,
                                                     formData);

            _mockAutomatedEligibilityChecksHelper.Setup(x => x.CheckAutomatedEligibility(process.TargetId, incomingTenantId, tenantId)).ReturnsAsync(false);
            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SoleToJointStates.AutomatedChecksFailed,
                                                 new List<string>() { SoleToJointPermittedTriggers.CloseProcess });
            process.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.SelectTenants);
            _mockAutomatedEligibilityChecksHelper.Verify(x => x.CheckAutomatedEligibility(process.TargetId, incomingTenantId, tenantId), Times.Once());
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
                { SoleToJointFormDataKeys.IncomingTenantId, incomingTenantId },
                { SoleToJointFormDataKeys.TenantId, tenantId },
            };

            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CheckAutomatedEligibility,
                                                     formData);
            _mockAutomatedEligibilityChecksHelper.Setup(x => x.CheckAutomatedEligibility(process.TargetId, incomingTenantId, tenantId)).ReturnsAsync(true);

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SoleToJointStates.AutomatedChecksPassed,
                                                 new List<string>() { SoleToJointPermittedTriggers.CheckManualEligibility });
            process.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.SelectTenants);
            _mockAutomatedEligibilityChecksHelper.Verify(x => x.CheckAutomatedEligibility(process.TargetId, incomingTenantId, tenantId), Times.Once());
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.SelectTenants, SoleToJointStates.AutomatedChecksPassed);
        }

        [Fact]
        public void ThrowsFormDataNotFoundExceptionOnCheckAutomatedEligibilityTrigger()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.SelectTenants);

            var tenantId = Guid.NewGuid();
            var formData = new Dictionary<string, object> { { SoleToJointFormDataKeys.TenantId, tenantId } };

            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CheckAutomatedEligibility,
                                                     formData);
            var expectedErrorMessage = $"The request's FormData is invalid: The form data keys supplied ({SoleToJointFormDataKeys.TenantId}) do not include the expected values ({SoleToJointFormDataKeys.IncomingTenantId}).";
            // Act
            Func<Task> func = async () => await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);
            // Assert
            func.Should().Throw<FormDataNotFoundException>().WithMessage(expectedErrorMessage);
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
                                                 new List<string> { "CheckTenancyBreach" });
            process.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.AutomatedChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.AutomatedChecksPassed, SoleToJointStates.ManualChecksPassed);
        }

        [Theory]
        [InlineData(SoleToJointFormDataKeys.BR11, "false")]
        [InlineData(SoleToJointFormDataKeys.BR12, "true")]
        [InlineData(SoleToJointFormDataKeys.BR13, "true")]
        [InlineData(SoleToJointFormDataKeys.BR15, "true")]
        [InlineData(SoleToJointFormDataKeys.BR16, "true")]
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
                                                 new List<string>() { SoleToJointPermittedTriggers.CloseProcess });
            process.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.AutomatedChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.AutomatedChecksPassed, SoleToJointStates.ManualChecksFailed);
        }

        [Fact]
        public void ThrowsFormDataNotFoundExceptionOnCheckManualEligibilityTrigger()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.AutomatedChecksPassed);

            var formData = new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.BR12, true },
                { SoleToJointFormDataKeys.BR13, true },
                { SoleToJointFormDataKeys.BR15, true },
                { SoleToJointFormDataKeys.BR16, true },
                { SoleToJointFormDataKeys.BR7, "false" },
                { SoleToJointFormDataKeys.BR8, "false" }
            };

            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CheckManualEligibility,
                                                     formData);
            var expectedErrorMessage = $"The request's FormData is invalid: The form data keys supplied ({String.Join(", ", formData.Keys.ToList())}) do not include the expected values ({SoleToJointFormDataKeys.BR11}).";
            // Act
            Func<Task> func = async () => await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);
            // Assert
            func.Should().Throw<FormDataNotFoundException>().WithMessage(expectedErrorMessage);
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
                new List<string> { SoleToJointPermittedTriggers.RequestDocumentsDes, SoleToJointPermittedTriggers.RequestDocumentsAppointment });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.ManualChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.ManualChecksPassed, SoleToJointStates.BreachChecksPassed);
        }

        [Theory]
        [InlineData(SoleToJointFormDataKeys.BR5, "true")]
        [InlineData(SoleToJointFormDataKeys.BR10, "true")]
        [InlineData(SoleToJointFormDataKeys.BR17, "true")]
        [InlineData(SoleToJointFormDataKeys.BR18, "true")]
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
                new List<string> { SoleToJointPermittedTriggers.CloseProcess });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.ManualChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.ManualChecksPassed, SoleToJointStates.BreachChecksFailed);
        }

        [Theory]
        [InlineData(SoleToJointFormDataKeys.BR5)]
        [InlineData(SoleToJointFormDataKeys.BR10)]
        [InlineData(SoleToJointFormDataKeys.BR17)]
        [InlineData(SoleToJointFormDataKeys.BR18)]
        public void ThrowsFormDataNotFoundExceptionOnOnTenancyBreachCheckWhenCheckIsMissed(string checkId)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.ManualChecksPassed);

            _tenancyBreachPassData.Remove(checkId);

            var triggerObject = CreateProcessTrigger(
                process, SoleToJointPermittedTriggers.CheckTenancyBreach, _tenancyBreachPassData);

            var expectedErrorMessage = $"The request's FormData is invalid: The form data keys supplied ({String.Join(", ", _tenancyBreachPassData.Keys.ToList())}) do not include the expected values ({checkId}).";

            // Act & assert
            _classUnderTest
                .Invoking(cut => cut.Process(triggerObject, process, _token))
                .Should().Throw<FormDataNotFoundException>().WithMessage(expectedErrorMessage);
        }

        #endregion

        #region Request Documents

        [Fact]
        public async Task ProcessStateIsUpdatedToDocumentsRequestedAppointmentWhenAppointmentDateTimeIsCorrectFormat()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.BreachChecksPassed);
            var formData = new Dictionary<string, object>() { { SoleToJointFormDataKeys.AppointmentDateTime, _fixture.Create<DateTime>() } };
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.RequestDocumentsAppointment, formData);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SoleToJointStates.DocumentsRequestedAppointment,
                new List<string> { SoleToJointPermittedTriggers.RescheduleDocumentsAppointment, SoleToJointPermittedTriggers.ReviewDocuments, SoleToJointPermittedTriggers.CloseProcess });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.BreachChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.BreachChecksPassed, SoleToJointStates.DocumentsRequestedAppointment);
        }

        [Fact]
        public void ThrowsFormDataNotFoundExceptionOnRequestDocumentsAppointmentWhenAppointmentDetailsAreNull()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.BreachChecksPassed);
            var formData = new Dictionary<string, object>();
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.RequestDocumentsAppointment, formData);

            var expectedErrorMessage = $"The request's FormData is invalid: The form data keys supplied () do not include the expected values ({SoleToJointFormDataKeys.AppointmentDateTime}).";

            // Act & assert
            _classUnderTest
                .Invoking(cut => cut.Process(trigger, process, _token))
                .Should().Throw<FormDataNotFoundException>().WithMessage(expectedErrorMessage);
        }

        #endregion

        #region Request documents via DES

        [Fact]
        public async Task ProcessStateIsUpdatedToDocumentsRequestedDesOnRequestDocumentsDesTrigger()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.BreachChecksPassed);
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.RequestDocumentsDes);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SoleToJointStates.DocumentsRequestedDes,
                new List<string> { SoleToJointPermittedTriggers.RequestDocumentsAppointment, SoleToJointPermittedTriggers.ReviewDocuments, SoleToJointPermittedTriggers.CloseProcess });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.BreachChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.BreachChecksPassed, SoleToJointStates.DocumentsRequestedDes);
        }

        #endregion

        #region Request documents via appointment

        [Fact]
        public async Task ProcessStateIsUpdatedToDocumentsRequestedAppointmentOnRequestDocumentsAppointmentTrigger()
        {
            // Arrange
            var appointmentDateTime = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);

            var process = CreateProcessWithCurrentState(SoleToJointStates.BreachChecksPassed);
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.RequestDocumentsAppointment, new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.AppointmentDateTime, appointmentDateTime }
            });

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SoleToJointStates.DocumentsRequestedAppointment,
                new List<string>
                {
                    SoleToJointPermittedTriggers.RescheduleDocumentsAppointment,
                    SoleToJointPermittedTriggers.ReviewDocuments,
                    SoleToJointPermittedTriggers.CloseProcess
                });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.BreachChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.BreachChecksPassed, SoleToJointStates.DocumentsRequestedAppointment);

            var stateData = (_lastSnsEvent.EventData.NewData as ProcessStateChangeData).StateData;
            stateData.Should().ContainKey(SoleToJointFormDataKeys.AppointmentDateTime);
        }

        #endregion

        #region Reschedule documents appointment

        [Theory]
        [InlineData(SoleToJointStates.DocumentsRequestedAppointment)]
        [InlineData(SoleToJointStates.DocumentsAppointmentRescheduled)]
        public async Task ProcessStateIsUpdatedToDocumentsAppointmentRescheduledOnRescheduleDocumentsAppointmentTrigger(string initialState)
        {
            // Arrange
            var appointmentDateTime = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);

            var process = CreateProcessWithCurrentState(initialState, new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.AppointmentDateTime, appointmentDateTime }
            });
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.RescheduleDocumentsAppointment, new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.AppointmentDateTime, appointmentDateTime }
            });

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SoleToJointStates.DocumentsAppointmentRescheduled,
                new List<string>
                {
                    SoleToJointPermittedTriggers.ReviewDocuments,
                    SoleToJointPermittedTriggers.RescheduleDocumentsAppointment,
                    SoleToJointPermittedTriggers.CloseProcess
                }
            );
            process.PreviousStates.Last().State.Should().Be(initialState);
            VerifyThatProcessUpdatedEventIsTriggered(initialState, SoleToJointStates.DocumentsAppointmentRescheduled);

            var stateData = (_lastSnsEvent.EventData.NewData as ProcessStateChangeData).StateData;
            stateData.Should().ContainKey(SoleToJointFormDataKeys.AppointmentDateTime);
        }

        #endregion

        #region Tenure Investigation

        [Fact]
        public async Task ProcessStateIsUpdatedToApplicationSubmittedOnSubmitApplicationTrigger()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.DocumentChecksPassed);
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.SubmitApplication);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SoleToJointStates.ApplicationSubmitted,
                new List<string> { SoleToJointPermittedTriggers.TenureInvestigation });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.DocumentChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.DocumentChecksPassed, SoleToJointStates.ApplicationSubmitted);
        }

        [Theory]
        [InlineData(SoleToJointFormDataValues.Appointment, SoleToJointStates.TenureInvestigationPassedWithInt)]
        [InlineData(SoleToJointFormDataValues.Approve, SoleToJointStates.TenureInvestigationPassed)]
        [InlineData(SoleToJointFormDataValues.Decline, SoleToJointStates.TenureInvestigationFailed)]
        public async Task ProcessStateIsUpdatedOnTenureInvestigationTrigger(string tenureInvestigationRecommendation, string expectedState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.ApplicationSubmitted);
            var formData = new Dictionary<string, object>
            {
                {  SoleToJointFormDataKeys.TenureInvestigationRecommendation, tenureInvestigationRecommendation }
            };
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.TenureInvestigation, formData);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, expectedState,
                new List<string> { /* TODO when next trigger is implemented */ }
            );
            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.ApplicationSubmitted);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.ApplicationSubmitted, expectedState);
        }

        [Fact]
        public void ThrowsFormDataNotFoundExceptionOnTenureInvestigationWhenRecommendationIsNull()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.ApplicationSubmitted);
            var formData = new Dictionary<string, object>();
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.TenureInvestigation, formData);

            var expectedErrorMessage = $"The request's FormData is invalid: The form data keys supplied () do not include the expected values ({SoleToJointFormDataKeys.TenureInvestigationRecommendation}).";

            // Act & assert
            _classUnderTest
                .Invoking(cut => cut.Process(trigger, process, _token))
                .Should().Throw<FormDataNotFoundException>().WithMessage(expectedErrorMessage);
        }

        [Fact]
        public void ThrowsFormDataInvalidExceptionOnTenureInvestigationWhenRecommendationIsNotOneOfCorrectValues()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.ApplicationSubmitted);
            var invalidRecommendation = "some invalid value";
            var formData = new Dictionary<string, object>
            {
                {  SoleToJointFormDataKeys.TenureInvestigationRecommendation, invalidRecommendation }
            };
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.TenureInvestigation, formData);

            var expectedErrorMessage = String.Format("The request's FormData is invalid: Tenure Investigation Recommendation must be one of: [{0}, {1}, {2}], but the value provided was: '{3}'.",
                                                     SoleToJointFormDataValues.Appointment,
                                                     SoleToJointFormDataValues.Approve,
                                                     SoleToJointFormDataValues.Decline,
                                                     invalidRecommendation);

            // Act & assert
            _classUnderTest
                .Invoking(cut => cut.Process(trigger, process, _token))
                .Should().Throw<FormDataInvalidException>().WithMessage(expectedErrorMessage);
        }

        #endregion
    }
}
