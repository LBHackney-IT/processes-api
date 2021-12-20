using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Infrastructure;
using ProcessesApi.V1.UseCase;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
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
        private readonly List<Action> _cleanup = new List<Action>();
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

        public SoleToJointServiceTests(MockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _classUnderTest = new SoleToJointService();
        }

        private async Task InsertDatatoDynamoDB(ProcessesDb entity)
        {
            await _dbFixture.SaveEntityAsync(entity).ConfigureAwait(false);
        }

        [Fact]
        public async Task InitialiseStateToSelectTenantsIfCurrentStateIsNotDefined()
        {
            // Arrange
            var process = _fixture.Build<Process>()
                                    .With(x => x.CurrentState, (ProcessState) null)
                                    .With(x => x.PreviousStates, new List<ProcessState>())
                                    .Create();
            var triggerObject = UpdateProcessState.Create
            (
                process.Id,
                process.TargetId,
                SoleToJointTriggers.StartApplication,
                _fixture.Create<Dictionary<string, object>>(),
                _fixture.Create<List<Guid>>(),
                process.RelatedEntities
            );
            // Act
            await _classUnderTest.Process(triggerObject, process).ConfigureAwait(false);
            // Assert
            process.CurrentState.State.Should().Be(SoleToJointStates.SelectTenants);
            process.PreviousStates.Should().BeEmpty();
            process.CurrentState.PermittedTriggers.Should().BeEquivalentTo(new List<string>() { SoleToJointPermittedTriggers.CheckEligibility });
            process.CurrentState.ProcessData.FormData.Should().BeEquivalentTo(triggerObject.FormData);
            process.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(triggerObject.Documents);
            process.CurrentState.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            process.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
        }

        [Fact]
        public async Task AddTenantToRelatedEntitiesOnCheckEligibilityTrigger()
        {
            // Arrange
            var process = _fixture.Build<Process>()
                                .With(x => x.CurrentState,
                                    _fixture.Build<ProcessState>()
                                        .With(x => x.State, SoleToJointStates.SelectTenants)
                                        .Create()
                                )
                                .Create();

            var incomingTenantId = Guid.NewGuid();
            var triggerObject = UpdateProcessState.Create
            (
                process.Id,
                process.TargetId,
                SoleToJointTriggers.CheckEligibility,
                new Dictionary<string, object> { { SoleToJointFormDataKeys.IncomingTenantId, incomingTenantId } },
                _fixture.Create<List<Guid>>(),
                process.RelatedEntities
            );
            // Act
            await _classUnderTest.Process(triggerObject, process).ConfigureAwait(false);

            // Assert
            process.RelatedEntities.Should().Contain(incomingTenantId);
        }
    }
}
