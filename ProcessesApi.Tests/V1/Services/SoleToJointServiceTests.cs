using AutoFixture;
using FluentAssertions;
using Moq;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.SoleToJoint;
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
using Hackney.Shared.Person;

namespace ProcessesApi.Tests.V1.Services
{
    [Collection("AppTest collection")]
    public class SoleToJointServiceTests : IDisposable
    {
        public SoleToJointService _classUnderTest;
        public Fixture _fixture = new Fixture();
        private readonly List<Action> _cleanup = new List<Action>();

        private Mock<ISoleToJointAutomatedEligibilityChecksHelper> _mockAutomatedEligibilityChecksHelper;
        private Mock<IGetPersonByIdHelper> _mockPersonByIdHelper;
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
            _mockPersonByIdHelper = new Mock<IGetPersonByIdHelper>();
            _mockAutomatedEligibilityChecksHelper = new Mock<ISoleToJointAutomatedEligibilityChecksHelper>();

            _classUnderTest = new SoleToJointService(new ProcessesSnsFactory(),
                                                     _mockSnsGateway.Object,
                                                     _mockAutomatedEligibilityChecksHelper.Object,
                                                     _mockPersonByIdHelper.Object);

            _mockSnsGateway
                .Setup(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<EntityEventSns, string, string>((ev, s1, s2) => _lastSnsEvent = ev);
        }

        private Person CreatePerson(Guid incomingTenantId)
        {
            return _fixture.Build<Person>()
                           .With(x => x.Id, incomingTenantId)
                           .Create();
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

        // List states & triggers that expect certain form data values
        [Theory]
        [InlineData(SoleToJointStates.AutomatedChecksFailed, SoleToJointPermittedTriggers.CloseProcess, new string[] { SoleToJointFormDataKeys.HasNotifiedResident })]
        [InlineData(SoleToJointStates.DocumentsRequestedDes, SoleToJointPermittedTriggers.CancelProcess, new string[] { SoleToJointFormDataKeys.Comment })]
        [InlineData(SoleToJointStates.SelectTenants, SoleToJointPermittedTriggers.CheckAutomatedEligibility, new string[] { SoleToJointFormDataKeys.IncomingTenantId, SoleToJointFormDataKeys.TenantId })]
        [InlineData(SoleToJointStates.AutomatedChecksPassed, SoleToJointPermittedTriggers.CheckManualEligibility, new string[] { SoleToJointFormDataKeys.BR11, SoleToJointFormDataKeys.BR12, SoleToJointFormDataKeys.BR13,
                                                                                                                                SoleToJointFormDataKeys.BR15, SoleToJointFormDataKeys.BR16, SoleToJointFormDataKeys.BR7, SoleToJointFormDataKeys.BR8 })]
        [InlineData(SoleToJointStates.ManualChecksPassed, SoleToJointPermittedTriggers.CheckTenancyBreach, new string[] { SoleToJointFormDataKeys.BR5, SoleToJointFormDataKeys.BR10, SoleToJointFormDataKeys.BR17, SoleToJointFormDataKeys.BR18 })]
        [InlineData(SoleToJointStates.BreachChecksPassed, SoleToJointPermittedTriggers.RequestDocumentsAppointment, new string[] { SoleToJointFormDataKeys.AppointmentDateTime })]
        [InlineData(SoleToJointStates.ApplicationSubmitted, SoleToJointPermittedTriggers.TenureInvestigation, new string[] { SoleToJointFormDataKeys.TenureInvestigationRecommendation })]
        public void ThrowsFormDataNotFoundException(string initialState, string trigger, string[] expectedFormDataKeys)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(initialState);

            var triggerObject = CreateProcessTrigger(process,
                                                     trigger,
                                                     new Dictionary<string, object>());

            var expectedErrorMessage = $"The request's FormData is invalid: The form data keys supplied () do not include the expected values ({String.Join(", ", expectedFormDataKeys)}).";
            // Act + Assert
            _classUnderTest.Invoking(x => x.Process(triggerObject, process, _token))
                           .Should().Throw<FormDataNotFoundException>().WithMessage(expectedErrorMessage);
        }

        #region Close or Cancel Process

        // List all states that CloseProcess can be triggered from
        [Theory]
        [InlineData(SoleToJointStates.AutomatedChecksFailed, true)]
        [InlineData(SoleToJointStates.ManualChecksFailed, true)]
        [InlineData(SoleToJointStates.BreachChecksFailed, true)]
        [InlineData(SoleToJointStates.AutomatedChecksFailed, false)]
        [InlineData(SoleToJointStates.ManualChecksFailed, false)]
        [InlineData(SoleToJointStates.BreachChecksFailed, false)]
        public async Task ProcessStateIsUpdatedToProcessClosedAndEventIsRaised(string fromState, bool hasReason)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(fromState);
            var formData = new Dictionary<string, object>()
            {
                { SoleToJointFormDataKeys.HasNotifiedResident, true }
            };
            if (hasReason) formData.Add(SoleToJointFormDataKeys.Reason, "this is a reason.");

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

        // List all states that CancelProcess can be triggered from
        [Theory]
        [InlineData(SoleToJointStates.DocumentsRequestedDes)]
        [InlineData(SoleToJointStates.DocumentsRequestedAppointment)]
        [InlineData(SoleToJointStates.DocumentsAppointmentRescheduled)]
        [InlineData(SoleToJointStates.TenureAppointmentRescheduled)]
        public async Task ProcessStateIsUpdatedToProcessCancelledAndProcessClosedEventIsRaised(string fromState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(fromState);
            var formData = new Dictionary<string, object>()
            {
                { SoleToJointFormDataKeys.Comment, "Some comment" }
            };

            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CancelProcess,
                                                     formData);

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SharedProcessStates.ProcessCancelled,
                                                 new List<string>());
            process.PreviousStates.LastOrDefault().State.Should().Be(fromState);

            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _lastSnsEvent.EventType.Should().Be(ProcessClosedEventConstants.EVENTTYPE);
        }

        # endregion

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
            var person = CreatePerson(incomingTenantId);
            _mockPersonByIdHelper.Setup(x => x.GetPersonById(incomingTenantId)).ReturnsAsync(person);
            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            var relatedEntity = process.RelatedEntities.Find(x => x.Id == incomingTenantId);
            relatedEntity.Should().NotBeNull();
            relatedEntity.TargetType.Should().Be(TargetType.person);
            relatedEntity.SubType.Should().Be(SubType.householdMember);
            relatedEntity.Description.Should().Be($"{person.FirstName} {person.Surname}");
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
            var person = CreatePerson(incomingTenantId);
            _mockPersonByIdHelper.Setup(x => x.GetPersonById(incomingTenantId)).ReturnsAsync(person);
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
            var person = CreatePerson(incomingTenantId);
            _mockPersonByIdHelper.Setup(x => x.GetPersonById(incomingTenantId)).ReturnsAsync(person);
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
                new List<string>
                {
                    SoleToJointPermittedTriggers.RescheduleDocumentsAppointment,
                    SoleToJointPermittedTriggers.ReviewDocuments,
                    SoleToJointPermittedTriggers.CancelProcess
                });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.BreachChecksPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.BreachChecksPassed, SoleToJointStates.DocumentsRequestedAppointment);
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
                new List<string>
                {
                    SoleToJointPermittedTriggers.RequestDocumentsAppointment,
                    SoleToJointPermittedTriggers.ReviewDocuments,
                    SoleToJointPermittedTriggers.CancelProcess
                });

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
                    SoleToJointPermittedTriggers.CancelProcess
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
                    SoleToJointPermittedTriggers.CancelProcess
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
        [InlineData(SoleToJointFormDataValues.Approve, SoleToJointStates.TenureInvestigationPassed)]
        [InlineData(SoleToJointFormDataValues.Decline, SoleToJointStates.TenureInvestigationFailed)]
        [InlineData(SoleToJointFormDataValues.Appointment, SoleToJointStates.TenureInvestigationPassedWithInt)]
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
                new List<string> { SoleToJointPermittedTriggers.ScheduleInterview }
            );
            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.ApplicationSubmitted);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.ApplicationSubmitted, expectedState);
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

        #region Schedule Interview

        [Fact]
        public async Task ProcessStateIsUpdatedToInterviewScheduledOnScheduleInterviewTrigger()
        {
            // Arrange
            var appointmentDateTime = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);
            var process = CreateProcessWithCurrentState(SoleToJointStates.TenureInvestigationPassedWithInt);
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.ScheduleInterview, new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.AppointmentDateTime, appointmentDateTime }
            });

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SoleToJointStates.InterviewScheduled,
                new List<string> { SoleToJointPermittedTriggers.RescheduleInterview, SoleToJointPermittedTriggers.HOApproval });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.TenureInvestigationPassedWithInt);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.TenureInvestigationPassedWithInt, SoleToJointStates.InterviewScheduled);
        }

        #endregion

        #region Reschedule Interview

        [Fact]
        public async Task ProcessStateIsUpdatedToInterviewRescheduledOnScheduleInterview()
        {
            // Arrange
            var appointmentDateTime = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);
            var process = CreateProcessWithCurrentState(SoleToJointStates.InterviewScheduled);
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.RescheduleInterview, new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.AppointmentDateTime, appointmentDateTime }
            });

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SoleToJointStates.InterviewRescheduled,
                new List<string> { SoleToJointPermittedTriggers.HOApproval, SoleToJointPermittedTriggers.CancelProcess });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.InterviewScheduled);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.InterviewScheduled, SoleToJointStates.InterviewRescheduled);
        }

        #endregion

        #region HOApproval

        [Theory]
        [InlineData(SoleToJointStates.InterviewScheduled)]
        [InlineData(SoleToJointStates.InterviewRescheduled)]
        public async Task ProcessStateIsUpdatedToHOApprovalPassed(string initialState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(initialState);
            var formData = new Dictionary<string, object>
            {
                {  SoleToJointFormDataKeys.HORecommendation, SoleToJointFormDataValues.Approve },
            };
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.HOApproval, formData);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SoleToJointStates.HOApprovalPassed,
                new List<string> { SoleToJointPermittedTriggers.ScheduleTenureAppointment }
            );
            process.PreviousStates.Last().State.Should().Be(initialState);
            VerifyThatProcessUpdatedEventIsTriggered(initialState, SoleToJointStates.HOApprovalPassed);
        }

        [Theory]
        [InlineData(SoleToJointStates.InterviewScheduled)]
        [InlineData(SoleToJointStates.InterviewRescheduled)]
        public async Task ProcessStateIsUpdatedToHOApprovalFailed(string initialState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(initialState);
            var formData = new Dictionary<string, object>
            {
                {  SoleToJointFormDataKeys.HORecommendation, SoleToJointFormDataValues.Decline }
            };
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.HOApproval, formData);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SoleToJointStates.HOApprovalFailed,
                new List<string> { SoleToJointPermittedTriggers.CancelProcess }
            );
            process.PreviousStates.Last().State.Should().Be(initialState);
            VerifyThatProcessUpdatedEventIsTriggered(initialState, SoleToJointStates.HOApprovalFailed);
        }

        [Theory]
        [InlineData(SoleToJointStates.InterviewScheduled)]
        [InlineData(SoleToJointStates.InterviewRescheduled)]
        public void ThrowsFormDataNotFoundExceptionOnHOApprovalWhenRecommendationIsNull(string initialState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(initialState);
            var formData = new Dictionary<string, object>();
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.HOApproval, formData);

            var expectedErrorMessage = $"The request's FormData is invalid: The form data keys supplied () do not include the expected values ({SoleToJointFormDataKeys.HORecommendation}).";

            // Act & assert
            _classUnderTest
                .Invoking(cut => cut.Process(trigger, process, _token))
                .Should().Throw<FormDataNotFoundException>().WithMessage(expectedErrorMessage);
        }

        [Theory]
        [InlineData(SoleToJointStates.InterviewScheduled)]
        [InlineData(SoleToJointStates.InterviewRescheduled)]
        public void ThrowsFormDataInvalidExceptionOnHousingApprovalWhenRecommendationIsNotOneOfCorrectValues(string initialState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(initialState);
            var invalidRecommendation = "some invalid value";
            var formData = new Dictionary<string, object>
            {
                {  SoleToJointFormDataKeys.HORecommendation, invalidRecommendation }
            };
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.HOApproval, formData);

            var expectedErrorMessage = String.Format("The request's FormData is invalid: Housing Officer Recommendation must be one of: [{0}, {1}] but the value provided was: '{2}'.",
                                                     SoleToJointFormDataValues.Approve,
                                                     SoleToJointFormDataValues.Decline,
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
            var process = CreateProcessWithCurrentState(SoleToJointStates.HOApprovalPassed);
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.ScheduleTenureAppointment, new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.AppointmentDateTime, appointmentDateTime }
            });

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SoleToJointStates.TenureAppointmentScheduled,
                new List<string> { SoleToJointPermittedTriggers.RescheduleTenureAppointment, SoleToJointPermittedTriggers.UpdateTenure }

             );

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.HOApprovalPassed);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.HOApprovalPassed, SoleToJointStates.TenureAppointmentScheduled);
        }


        #endregion

        #region Reschedule Tenure Appointment
        [Fact]
        public async Task ProcessStateIsUpdatedToRescheduleTenureAppointmentOnHOApprovalPassed()
        {
            // Arrange
            var appointmentDateTime = DateTime.UtcNow.ToString("s", CultureInfo.InvariantCulture);
            var process = CreateProcessWithCurrentState(SoleToJointStates.TenureAppointmentScheduled);
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.RescheduleTenureAppointment, new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.AppointmentDateTime, appointmentDateTime }
            });

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(
                process, trigger, SoleToJointStates.TenureAppointmentRescheduled,
                new List<string> { SoleToJointPermittedTriggers.UpdateTenure, SoleToJointPermittedTriggers.CancelProcess }
            );

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.TenureAppointmentScheduled);
            VerifyThatProcessUpdatedEventIsTriggered(SoleToJointStates.TenureAppointmentScheduled, SoleToJointStates.TenureAppointmentRescheduled);
        }

        #endregion

        #region Update Tenure

        [Theory]
        [InlineData(SoleToJointStates.TenureAppointmentScheduled, true)]
        [InlineData(SoleToJointStates.TenureAppointmentRescheduled, true)]
        [InlineData(SoleToJointStates.TenureAppointmentScheduled, false)]
        [InlineData(SoleToJointStates.TenureAppointmentRescheduled, false)]
        public async Task ProcessStateIsUpdatedToProcessCompletedAndEventIsRaisedOnUpdateTenure(string initialState, bool hasReason)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(initialState);
            var formData = new Dictionary<string, object>()
            {
                { SoleToJointFormDataKeys.HasNotifiedResident, true }
            };
            if (hasReason) formData.Add(SoleToJointFormDataKeys.Reason, "this is a reason.");

            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.UpdateTenure,
                                                     formData);

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject,
                                                 SoleToJointStates.TenureUpdated,
                                                 new List<string>());
            process.PreviousStates.LastOrDefault().State.Should().Be(initialState);

            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _lastSnsEvent.EventType.Should().Be(ProcessCompletedEventConstants.EVENTTYPE);
        }

        #endregion
    }
}
