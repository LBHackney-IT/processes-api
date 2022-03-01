using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Moq;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Hackney.Core.JWT;
using Hackney.Core.Sns;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure.JWT;
using Xunit;

namespace ProcessesApi.Tests.V1.UseCase
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
            { SoleToJointFormDataKeys.BR16, "false" }
        };


        private Mock<ISoleToJointGateway> _mockSTJGateway;
        private Mock<ISnsGateway> _mockSnsGateway;

        private readonly List<Action> _cleanup = new List<Action>();
        private Token _token = new Token();

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
            _mockSTJGateway = new Mock<ISoleToJointGateway>();
            _mockSnsGateway = new Mock<ISnsGateway>();
            _classUnderTest = new SoleToJointService(_mockSTJGateway.Object, new ProcessesSnsFactory(), _mockSnsGateway.Object);
        }

        private Process CreateProcessWithCurrentState(string currentState)
        {
            return _fixture.Build<Process>()
                            .With(x => x.CurrentState,
                                    _fixture.Build<ProcessState>()
                                        .With(x => x.State, currentState)
                                        .Create()
                                )
                            .Create();
        }

        private UpdateProcessState CreateProcessTrigger(Process process, string trigger, Dictionary<string, object> formData)
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

        private void CurrentStateShouldContainCorrectData(Process process, string expectedCurrentState, List<string> expectedTriggers, UpdateProcessState triggerObject)
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
                                                     SoleToJointInternalTriggers.StartApplication,
                                                     _fixture.Create<Dictionary<string, object>>());
            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);
            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 SoleToJointStates.SelectTenants,
                                                 new List<string>() { SoleToJointPermittedTriggers.CheckEligibility },
                                                 triggerObject);
            process.PreviousStates.Should().BeEmpty();
        }

        [Fact]
        public async Task AddTenantToRelatedEntitiesOnCheckEligibilityTrigger()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.SelectTenants);

            var incomingTenantId = Guid.NewGuid();
            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CheckEligibility,
                                                     new Dictionary<string, object> { { SoleToJointFormDataKeys.IncomingTenantId, incomingTenantId } });
            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            process.RelatedEntities.Should().Contain(incomingTenantId);
        }

        [Fact]
        public async Task CurrentStateIsUpdatedToAutomatedChecksFailedWhenCheckEligibilityReturnsFalse()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.SelectTenants);

            var incomingTenantId = Guid.NewGuid();
            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CheckEligibility,
                                                     new Dictionary<string, object> { { SoleToJointFormDataKeys.IncomingTenantId, incomingTenantId } });

            _mockSTJGateway.Setup(x => x.CheckEligibility(process.TargetId, incomingTenantId)).ReturnsAsync(false);

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 SoleToJointStates.AutomatedChecksFailed,
                                                 new List<string>() { SoleToJointPermittedTriggers.CancelProcess },
                                                 triggerObject);
            process.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.SelectTenants);
            _mockSTJGateway.Verify(x => x.CheckEligibility(process.TargetId, incomingTenantId), Times.Once());
        }

        [Fact]
        public async Task ProcessStateIsUpdatedToAutomatedChecksPassedWhenCheckEligibilityReturnsTrue()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.SelectTenants);

            var incomingTenantId = Guid.NewGuid();
            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CheckEligibility,
                                                     new Dictionary<string, object> { { SoleToJointFormDataKeys.IncomingTenantId, incomingTenantId } });
            _mockSTJGateway.Setup(x => x.CheckEligibility(process.TargetId, incomingTenantId)).ReturnsAsync(true);

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 SoleToJointStates.AutomatedChecksPassed,
                                                 new List<string>() { SoleToJointPermittedTriggers.CheckManualEligibility },
                                                 triggerObject);
            process.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.SelectTenants);
            _mockSTJGateway.Verify(x => x.CheckEligibility(process.TargetId, incomingTenantId), Times.Once());
        }

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
                                                 SoleToJointStates.ManualChecksPassed,
                                                 new List<string>(), // TODO: Update when next state is implemented
                                                 triggerObject);
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
                                                 SoleToJointStates.ManualChecksFailed,
                                                 new List<string>() { SoleToJointPermittedTriggers.CancelProcess },
                                                 triggerObject);
            process.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.AutomatedChecksPassed);
        }

        // List all states where CancelProcess can be triggered from
        [Theory]
        [InlineData(SoleToJointStates.AutomatedChecksFailed)]
        [InlineData(SoleToJointStates.ManualChecksFailed)]
        public async Task ProcessStateIsUpdatedToCloseProcess(string fromState)
        {
            // Arrange
            var process = CreateProcessWithCurrentState(fromState);

            var formData = _manualEligibilityPassData;
            var triggerObject = CreateProcessTrigger(process,
                                                     SoleToJointPermittedTriggers.CancelProcess,
                                                     new Dictionary<string, object>());
            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            CurrentStateShouldContainCorrectData(process,
                                                 SoleToJointStates.ProcessCancelled,
                                                 new List<string>(),
                                                 triggerObject);
            process.PreviousStates.LastOrDefault().State.Should().Be(fromState);
        }

        [Fact]
        public async Task ProcessStoppedEventIsRaisedWhenAutomaticEligibilityChecksFail()
        {
            // Arrange
            var process = CreateProcessWithCurrentState(SoleToJointStates.SelectTenants);
            var incomingTenantId = Guid.NewGuid();

            var triggerObject = CreateProcessTrigger(
                process,
                SoleToJointPermittedTriggers.CheckEligibility,
                new Dictionary<string, object> { { SoleToJointFormDataKeys.IncomingTenantId, incomingTenantId } });

            var snsEvent = new EntityEventSns();

            _mockSTJGateway.Setup(x => x.CheckEligibility(process.TargetId, incomingTenantId)).ReturnsAsync(false);
            _mockSnsGateway
                .Setup(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()))
                .Callback<EntityEventSns, string, string>((ev, s1, s2) => snsEvent = ev);

            // Act
            await _classUnderTest.Process(triggerObject, process, _token).ConfigureAwait(false);

            // Assert
            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);
            snsEvent.EventType.Should().Be(ProcessStoppedEventConstants.EVENTTYPE);
        }
    }
}
