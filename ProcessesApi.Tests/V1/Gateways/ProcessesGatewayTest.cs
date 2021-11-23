using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Shared;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Infrastructure;
using ProcessesApi.V1.Infrastructure.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
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

        private async Task<(ProcessesDb, UpdateProcessQuery, UpdateProcessQueryObject)> SetUpUpdateQuery()
        {
            var originalProcess = _fixture.Build<ProcessesDb>()
                                .With(x => x.VersionNumber, (int?) null)
                                .Create();
            await InsertDatatoDynamoDB(originalProcess).ConfigureAwait(false);

            var query = _fixture.Build<UpdateProcessQuery>()
                                .With(x => x.ProcessName, originalProcess.ProcessName)
                                .With(x => x.Id, originalProcess.Id)
                                .Create();
            var queryObject = _fixture.Create<UpdateProcessQueryObject>();
            return (originalProcess, query, queryObject);
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
            var entity = _fixture.Build<Process>()
                                .With(x => x.VersionNumber, (int?) null)
                                .Create();
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
            var query = _fixture.Create<CreateProcessQuery>();
            var processName = "test-process";
            // Act
            var process = await _classUnderTest.CreateNewProcess(query, processName).ConfigureAwait(false);
            // Assert
            var processDb = await _dynamoDb.LoadAsync<ProcessesDb>(process.Id).ConfigureAwait(false);
            processDb.Should().BeEquivalentTo(query.ToDatabase(), config => config.Excluding(x => x.VersionNumber)
                                                                                  .Excluding(y => y.ProcessName)
                                                                                  .Excluding(z => z.CurrentState.CreatedAt)
                                                                                  .Excluding(a => a.CurrentState.UpdatedAt));
            processDb.ProcessName.Should().Be(processName);
            processDb.CurrentState.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            processDb.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);

            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync", Times.Once());

            _cleanup.Add(async () => await _dynamoDb.DeleteAsync<ProcessesDb>(process.Id).ConfigureAwait(false));
        }

        [Fact]
        public async Task UpdateProcessSuccessfullyUpdatesProcess()
        {
            // Arrange
            (var originalProcess, var query, var queryObject) = await SetUpUpdateQuery().ConfigureAwait(false);
            // Act
            var updatedProcess = await _classUnderTest.UpdateProcess(queryObject, query, 0).ConfigureAwait(false);
            // Assert
            updatedProcess.CurrentState.Should().NotBe(originalProcess.CurrentState);
            updatedProcess.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(queryObject.Documents);
            updatedProcess.CurrentState.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            updatedProcess.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);

            updatedProcess.PreviousStates.LastOrDefault().Should().BeEquivalentTo(originalProcess.CurrentState, c => c.Excluding(x => x.ProcessData.FormData));

            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for ID: {query.Id}", Times.Once());
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync to update ID: {query.Id}", Times.Once());
        }

        [Fact]
        public async Task UpdateProcessThrowsExceptionOnVersionConflict()
        {
            // Arrange
            (var originalProcess, var query, var queryObject) = await SetUpUpdateQuery().ConfigureAwait(false);
            var ifMatch = 5;
            // Act
            Func<Task<Process>> func = async () => await _classUnderTest.UpdateProcess(queryObject, query, ifMatch).ConfigureAwait(false);
            // Assert
            func.Should().Throw<VersionNumberConflictException>().Where(x => (x.IncomingVersionNumber == ifMatch) && (x.ExpectedVersionNumber == 0));

            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for ID: {query.Id}", Times.Once());
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync to update ID: {query.Id}", Times.Never());
        }
    }
}
