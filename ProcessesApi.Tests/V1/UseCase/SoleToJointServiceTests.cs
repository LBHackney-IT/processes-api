using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure;
using ProcessesApi.V1.UseCase;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
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
            var process = Process.Create(Guid.NewGuid(), new List<ProcessState>(), null, Guid.NewGuid(), new List<Guid>(), ProcessNamesConstants.SoleToJoint, null);
            var processData = new ProcessData(new JsonElement(), new List<Guid>() { Guid.NewGuid()});
            var triggerObject = UpdateProcessState.Create(process.Id, process.TargetId, SoleToJointTriggers.StartApplication, processData.FormData,processData.Documents, process.RelatedEntities);
            // Act
            await _classUnderTest.Process(triggerObject, process).ConfigureAwait(false);
            // Assert
            process.PreviousStates.Should().BeEmpty();
            process.CurrentState.State.Should().Be(SoleToJointStates.SelectTenants);
            process.CurrentState.PermittedTriggers.Should().BeEquivalentTo(new List<string>() { SoleToJointPermittedTriggers.CheckEligibility });
            process.CurrentState.ProcessData.FormData.Should().Be(triggerObject.FormData);
            process.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(triggerObject.Documents);
            process.CurrentState.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            process.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
        }

        [Fact]
        public async Task AddTenantToRelatedEntities()
        {
            // Arrange
            var currentState = new ProcessState(SoleToJointStates.SelectTenants, (new[] { SoleToJointTriggers.CheckEligibility }).ToList(), new Assignment(), new ProcessData(new JsonElement(), new List<Guid>()), DateTime.UtcNow, DateTime.UtcNow);
            var process = Process.Create(Guid.NewGuid(), new List<ProcessState>(), currentState, Guid.NewGuid(), new List<Guid>(), ProcessNamesConstants.SoleToJoint, null);
            await InsertDatatoDynamoDB(process.ToDatabase()).ConfigureAwait(false);
            var formDataValue = Guid.NewGuid();
            var dictionary = new Dictionary<string, string>()
            {
                {"incomingTenantId", formDataValue.ToString() }
            };
            var formData = JsonSerializer.Serialize(dictionary);
            var convertFormData = JsonDocument.Parse(formData).RootElement;
            var triggerObject = UpdateProcessState.Create(process.Id, null, SoleToJointTriggers.CheckEligibility, convertFormData, process.CurrentState.ProcessData.Documents, null);
            // Act
            await _classUnderTest.Process(triggerObject, process).ConfigureAwait(false);

            //Assert
            process.RelatedEntities.Should().Contain(formDataValue);
        }
    }
}
