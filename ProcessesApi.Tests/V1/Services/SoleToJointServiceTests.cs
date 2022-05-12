using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
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
using ProcessesApi.V1.Infrastructure.Extensions;

namespace ProcessesApi.Tests.V1.Services
{
    [Collection("AppTest collection")]
    public class SoleToJointServiceTests : IDisposable
    {
        public SoleToJointService _classUnderTest;
        public Fixture _fixture = new Fixture();
        private readonly IDynamoDbFixture _dbFixture;
        private IDynamoDBContext _dynamoDb => _dbFixture.DynamoDbContext;

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

        private Mock<ISoleToJointAutomatedEligibilityChecksHelper> _mockAutomatedEligibilityChecksHelper;
        private Mock<ISnsGateway> _mockSnsGateway;

        private readonly List<Action> _cleanup = new List<Action>();
        private readonly Token _token = new Token();
        private EntityEventSns _lastSnsEvent = new EntityEventSns();

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
            _dbFixture = appFactory.DynamoDbFixture;
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

        private UpdateProcessState CreateProcessTrigger(Process process, string trigger, Dictionary<string, object> formData = null)
        {
            return UpdateProcessState.Create
            (
                process.Id,
                process.TargetId,
                trigger,
                formData,
                _fixture.Create<List<Guid>>(),
                process.RelatedEntities
            );
        }

        private void CurrentStateShouldContainCorrectData(Process process, UpdateProcessState triggerObject, string expectedCurrentState, List<string> expectedTriggers)
        {
            process.CurrentState.State.Should().Be(expectedCurrentState);
            process.CurrentState.PermittedTriggers.Should().BeEquivalentTo(expectedTriggers);
            process.CurrentState.ProcessData.FormData.Should().BeEquivalentTo(triggerObject.FormData);
            process.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(triggerObject.Documents);
            process.CurrentState.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            process.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
        }

        [Fact]
        public async Task InitialiseStateToSelectTenantsIfCurrentStateIsNotDefined()
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
        }

        // List all states where CancelProcess can be triggered from
        [Theory]
        [InlineData(SoleToJointStates.AutomatedChecksFailed)]
        [InlineData(SoleToJointStates.ManualChecksFailed)]
        [InlineData(SoleToJointStates.BreachChecksFailed)]
        public async Task ProcessStateIsUpdatedToCloseProcess(string fromState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(fromState);

            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CancelProcess,
                                                     new Dictionary<string, object>());
            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 triggerObject, SoleToJointStates.ProcessCancelled, new List<string>());
            process.PreviousStates.LastOrDefault().State.Should().Be(fromState);
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
                                                 new List<string>() { SoleToJointPermittedTriggers.CancelProcess });
            process.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.SelectTenants);
            _mockAutomatedEligibilityChecksHelper.Verify(x => x.CheckAutomatedEligibility(process.TargetId, incomingTenantId, tenantId), Times.Once());
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

        [Fact]
        public async Task ProcessClosedEventIsRaisedWhenAutomaticEligibilityChecksFail()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.SelectTenants);
            var incomingTenantId = Guid.NewGuid();
            var tenantId = Guid.NewGuid();

            var triggerObject = CreateProcessTrigger(
                process,
                SoleToJointPermittedTriggers.CheckAutomatedEligibility,
                new Dictionary<string, object>
                {
                    { SoleToJointFormDataKeys.IncomingTenantId, incomingTenantId },
                    { SoleToJointFormDataKeys.TenantId, tenantId }
                });

            var snsEvent = new EntityEventSns();

            _mockAutomatedEligibilityChecksHelper.Setup(x => x.CheckAutomatedEligibility(process.TargetId, incomingTenantId, tenantId)).ReturnsAsync(false);
            _mockSnsGateway
                .Setup(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<EntityEventSns, string, string>((ev, s1, s2) => snsEvent = ev);

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            snsEvent.EventType.Should().Be(ProcessClosedEventConstants.EVENTTYPE);
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
                                                 new List<string>() { SoleToJointPermittedTriggers.CancelProcess });
            process.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.AutomatedChecksPassed);
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


        [Fact]
        public async Task ProcessClosedEventIsRaisedWhenManualEligibilityChecksFail()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.AutomatedChecksPassed);

            var eligibilityFormData = _manualEligibilityPassData;
            eligibilityFormData[SoleToJointFormDataKeys.BR11] = "false";

            var triggerObject = CreateProcessTrigger(process,
                SoleToJointPermittedTriggers.CheckManualEligibility,
                eligibilityFormData);

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _lastSnsEvent.EventType.Should().Be(ProcessClosedEventConstants.EVENTTYPE);
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
                new List<string> { SoleToJointPermittedTriggers.CancelProcess });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.ManualChecksPassed);
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

        [Fact]
        public async Task ProcessClosedEventIsRaisedWhenTenancyBreachChecksFail()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.ManualChecksPassed);

            _tenancyBreachPassData[SoleToJointFormDataKeys.BR5] = "true";

            var trigger = CreateProcessTrigger(
                process, SoleToJointPermittedTriggers.CheckTenancyBreach, _tenancyBreachPassData);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _lastSnsEvent.EventType.Should().Be(ProcessClosedEventConstants.EVENTTYPE);
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
                new List<string> { SoleToJointPermittedTriggers.RescheduleDocumentsAppointment /*Add next state here*/ });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.BreachChecksPassed);
        }

        [Fact]
        public async Task ProcessUpdatedEventIsRasiedWhenDocumentsRequestedAppointmentIsBooked()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.BreachChecksPassed);
            var formData = new Dictionary<string, object>() { { SoleToJointFormDataKeys.AppointmentDateTime, _fixture.Create<DateTime>() } };
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.RequestDocumentsAppointment, formData);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _lastSnsEvent.EventType.Should().Be(ProcessUpdatedEventConstants.EVENTTYPE);
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

        [Fact]
        public void ThrowsFormDataFormatExceptionOnRequestDocumentsAppointmentWhenAppointmentDateTimeIsNotCorrectFormat()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.BreachChecksPassed);
            var incorrectDateTime = _fixture.Create<int>();
            var formData = new Dictionary<string, object>() { { SoleToJointFormDataKeys.AppointmentDateTime, incorrectDateTime } };
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.RequestDocumentsAppointment, formData);

            var expectedErrorMessage = $"The request's FormData is invalid: The appointment datetime provided ({incorrectDateTime}) is not in the correct format.";

            // Act & assert
            _classUnderTest
                .Invoking(cut => cut.Process(trigger, process, _token))
                .Should().Throw<FormDataFormatException>().WithMessage(expectedErrorMessage);
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
                new List<string> { SoleToJointPermittedTriggers.RequestDocumentsAppointment /* TODO: Update permitted triggers when implemented */ });

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.BreachChecksPassed);
        }

        [Fact]
        public async Task ProcessUpdatedEventIsRaisedWhenDocumentsRequestedViaDes()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.BreachChecksPassed);
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.RequestDocumentsDes);

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            _lastSnsEvent.EventType.Should().Be(ProcessUpdatedEventConstants.EVENTTYPE);
            _lastSnsEvent.EventData.NewData.Should().BeOfType<Message>();
        }

        #endregion Request documents via DES

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
                new List<string> { SoleToJointPermittedTriggers.RescheduleDocumentsAppointment /* TODO: Update permitted when implemented */});

            process.PreviousStates.Last().State.Should().Be(SoleToJointStates.BreachChecksPassed);
        }

        [Fact]
        public async Task ProcessUpdatedEventIsRaisedWhenDocumentsRequestedViaAppointment()
        {
            // Arrange
            var appointmentDateTime = DateTime.UtcNow;

            var process = CreateProcessWithCurrentState(SoleToJointStates.BreachChecksPassed);
            var trigger = CreateProcessTrigger(
                process, SoleToJointPermittedTriggers.RequestDocumentsAppointment,
                new Dictionary<string, object>
                {
                    { SoleToJointFormDataKeys.AppointmentDateTime, appointmentDateTime }
                });

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);

            _lastSnsEvent.EventType.Should().Be(ProcessUpdatedEventConstants.EVENTTYPE);
            _lastSnsEvent.EventData.NewData.Should().BeOfType<Message>();


        }

        #endregion Request documents via appointment

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
                new List<string> { SoleToJointPermittedTriggers.RescheduleDocumentsAppointment });

            process.PreviousStates.Last().State.Should().Be(initialState);
        }

        [Fact]
        public async Task ProcessUpdatedEventIsRaisedWhenDocumentsAppointmentIsRescheduled()
        {
            // Arrange
            var oldAppointmentDateTime = DateTime.UtcNow.ToIsoString();
            var newAppointmentDateTime = DateTime.UtcNow.AddDays(1).ToIsoString();

            var process = CreateProcessWithCurrentState(SoleToJointStates.DocumentsRequestedAppointment, new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.AppointmentDateTime, oldAppointmentDateTime }
            });
            var trigger = CreateProcessTrigger(process, SoleToJointPermittedTriggers.RescheduleDocumentsAppointment, new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.AppointmentDateTime, newAppointmentDateTime }
            });

            // Act
            await _classUnderTest.Process(trigger, process, _token).ConfigureAwait(false);

            // Assert
            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);

            _lastSnsEvent.EventType.Should().Be(ProcessUpdatedEventConstants.EVENTTYPE);
            _lastSnsEvent.EventData.NewData.Should().BeOfType<Message>();

        }

        #endregion Reschedule documents appointment
    }
}
