using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Xunit;

namespace ProcessesApi.Tests.V1.Gateways
{
    [Collection("AppTest collection")]
    public class ProcessesGatewayTests : IDisposable
    {
        private readonly Fixture _fixture = new Fixture();
        private readonly IDynamoDbFixture _dbFixture;
        private IDynamoDBContext _dynamoDb => _dbFixture.DynamoDbContext;
        private ProcessesGateway _classUnderTest;
        private readonly List<Action> _cleanup = new List<Action>();
        private readonly Mock<ILogger<ProcessesGateway>> _logger;


        public ProcessesGatewayTests(MockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _logger = new Mock<ILogger<ProcessesGateway>>();
            _classUnderTest = new ProcessesGateway(_dynamoDb, _logger.Object);
        }

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

        private async Task InsertDatatoDynamoDB(ProcessesDb entity)
        {
            await _dbFixture.SaveEntityAsync(entity).ConfigureAwait(false);
        }

        [Fact]
        public async Task GetProcessByIdReturnsNullIfEntityDoesntExist()
        {
            var id = Guid.NewGuid();
            var response = await _classUnderTest.GetProcessById(id).ConfigureAwait(false);
            response.Should().BeNull();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for ID: {id}", Times.Once());
        }

        [Fact]
        public async Task GetProcessByIdReturnsTheProcessIfItExists()
        {
            var entity = Process.Create(Guid.NewGuid(), new List<ProcessState>(), null, Guid.NewGuid(), new List<Guid>() { Guid.NewGuid() }, ProcessNamesConstants.SoleToJoint, null);
            await InsertDatatoDynamoDB(entity.ToDatabase()).ConfigureAwait(false);
            var response = await _classUnderTest.GetProcessById(entity.Id).ConfigureAwait(false);
            response.Should().BeEquivalentTo(entity, config => config.Excluding(y => y.VersionNumber));
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for ID: {entity.Id}", Times.Once());
        }

        [Fact]
        public void GetProcessByIdExceptionIsThrown()
        {
            // Arrange
            var mockDynamoDb = new Mock<IDynamoDBContext>();
            _classUnderTest = new ProcessesGateway(mockDynamoDb.Object, _logger.Object);

            var id = Guid.NewGuid();
            var exception = new ApplicationException("Test Exception");

            mockDynamoDb.Setup(x => x.LoadAsync<ProcessesDb>(id, default))
                     .ThrowsAsync(exception);
            // Act
            Func<Task<Process>> func = async () => await _classUnderTest.GetProcessById(id).ConfigureAwait(false);
            // Assert
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
            mockDynamoDb.Verify(x => x.LoadAsync<ProcessesDb>(id, default), Times.Once);
        }

        [Fact]
        public async Task CreateNewProcessSucessfullySavesProcess()
        {
            // Arrange
            var processData = new ProcessData(new JsonElement(), new List<Guid>());
            var processState = new ProcessState(SoleToJointStates.ApplicationInitialised,
                                                (new[] { SoleToJointTriggers.StartApplication }).ToList(),
                                                new Assignment(),
                                                processData,
                                                DateTime.UtcNow,
                                                DateTime.UtcNow);
            var processObject = Process.Create(Guid.NewGuid(), new List<ProcessState>(), processState, Guid.NewGuid(), new List<Guid>(), ProcessNamesConstants.SoleToJoint, null);
            // Act
            var process = await _classUnderTest.SaveProcess(processObject).ConfigureAwait(false);
            // Assert
            var processDb = await _dynamoDb.LoadAsync<ProcessesDb>(process.Id).ConfigureAwait(false);
            processDb.Should().BeEquivalentTo(processObject.ToDatabase(), config => config.Excluding(x => x.VersionNumber)
                                                                                   .Excluding(z => z.CurrentState.CreatedAt)
                                                                                   .Excluding(a => a.CurrentState.UpdatedAt));
            processDb.VersionNumber.Should().Be(0);
            processDb.CurrentState.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            processDb.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);

            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync for id {process.Id}", Times.Once());

            _cleanup.Add(async () => await _dynamoDb.DeleteAsync<ProcessesDb>(process.Id).ConfigureAwait(false));
        }

        [Fact]
        public async Task UpdateProcessSuccessfullySavesProcess()
        {
            // Arrange
            //var originalProcessCurrentState = ProcessState.Create(SoleToJointStates.ApplicationInitialised,
            //                                       (new[] { SoleToJointTriggers.StartApplication }).ToList(),
            //                                       new Assignment(),
            //                                       new ProcessData(new System.Text.Json.JsonElement(),
            //                                       new List<Guid>()),
            //                                       DateTime.UtcNow,
            //                                       DateTime.UtcNow);
            var processState = new ProcessState(SoleToJointStates.ApplicationInitialised, (new[] { SoleToJointTriggers.StartApplication }).ToList(), new Assignment(), new ProcessData(new JsonElement(), new List<Guid>()), DateTime.UtcNow, DateTime.UtcNow);
            var originalProcess = Process.Create(Guid.NewGuid(), new List<ProcessState>(), processState, Guid.NewGuid(), new List<Guid>(), ProcessNamesConstants.SoleToJoint, null);
          
            await InsertDatatoDynamoDB(originalProcess.ToDatabase()).ConfigureAwait(false);
            var updateProcessCurrentState = ProcessState.Create(SoleToJointStates.SelectTenants,
                                                   (new[] { SoleToJointTriggers.CheckEligibility }).ToList(),
                                                   new Assignment(),
                                                   new ProcessData(new System.Text.Json.JsonElement(),
                                                   new List<Guid>()),
                                                   DateTime.UtcNow,
                                                   DateTime.UtcNow);
            var updateObject = Process.Create(originalProcess.Id, new List<ProcessState>(), updateProcessCurrentState, Guid.NewGuid(), new List<Guid>(), ProcessNamesConstants.SoleToJoint, 0);
          
            // Act
            var updatedProcess = await _classUnderTest.SaveProcess(updateObject).ConfigureAwait(false);
            // Assert
            updatedProcess.Should().BeEquivalentTo(updateObject, c => c.Excluding(y => y.VersionNumber));
            updatedProcess.VersionNumber.Should().Be(1);

            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync for id {updateObject.Id}", Times.Once());
        }
    }
}
