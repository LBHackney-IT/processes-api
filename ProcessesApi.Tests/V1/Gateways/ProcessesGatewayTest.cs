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
using ProcessesApi.V1.UseCase.Exceptions;
using System;
using System.Collections.Generic;
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


        public ProcessesGatewayTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _logger = new Mock<ILogger<ProcessesGateway>>();
            _classUnderTest = new ProcessesGateway(_dbFixture.DynamoDbContext, _logger.Object);
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
        public async Task SaveProcessSucessfullySavesNewProcessToDatabase()
        {
            // Arrange
            var process = _fixture.Build<Process>()
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();
            // Act
            await _classUnderTest.SaveProcess(process).ConfigureAwait(false);
            // Assert
            var processDb = await _dynamoDb.LoadAsync<ProcessesDb>(process.Id).ConfigureAwait(false);
            processDb.Should().BeEquivalentTo(process, config => config.Excluding(x => x.VersionNumber));
            processDb.VersionNumber.Should().Be(0);
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync for id {process.Id}", Times.Once());

            _cleanup.Add(async () => await _dynamoDb.DeleteAsync<ProcessesDb>(process.Id).ConfigureAwait(false));
        }

        [Fact]
        public async Task SaveProcessSuccessfullyOverwritesExistingProcess()
        {
            // Arrange
            var originalProcess = _fixture.Build<Process>()
                                    .With(x => x.VersionNumber, (int?) null)
                                    .Create();
            await InsertDatatoDynamoDB(originalProcess.ToDatabase()).ConfigureAwait(false);

            var updateObject = _fixture.Build<Process>()
                                    .With(x => x.Id, originalProcess.Id)
                                    .With(x => x.VersionNumber, 0)
                                    .Create();

            // Act
            var updatedProcess = await _classUnderTest.SaveProcess(updateObject).ConfigureAwait(false);
            // Assert
            updatedProcess.Should().BeEquivalentTo(updateObject, c => c.Excluding(x => x.VersionNumber));
            updatedProcess.VersionNumber.Should().Be(1);

            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync for id {updateObject.Id}", Times.Once());

            _cleanup.Add(async () => await _dynamoDb.DeleteAsync<ProcessesDb>(originalProcess.Id).ConfigureAwait(false));
        }

        [Fact]
        public async Task UpdateProcessByIdSuccessfullySaves()
        {
            var originalProcess = _fixture.Build<Process>()
                                   .With(x => x.VersionNumber, (int?) null)
                                   .Create();
            await InsertDatatoDynamoDB(originalProcess.ToDatabase()).ConfigureAwait(false);

            var updateProcessRequest = _fixture.Create<UpdateProcessByIdRequestObject>();

            var updatedProcessQuery = _fixture.Build<ProcessQuery>()
                        .With(x => x.Id, originalProcess.Id)
                        .Create();

            var response = await _classUnderTest.UpdateProcessById(updatedProcessQuery, updateProcessRequest, 0).ConfigureAwait(false);

            //CurrentState data changed
            response.CurrentState.ProcessData.FormData.Should().BeEquivalentTo(updateProcessRequest.FormData);
            response.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(updateProcessRequest.Documents);
            response.CurrentState.Assignment.Should().BeEquivalentTo(updateProcessRequest.Assignment);
            response.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            response.VersionNumber.Should().Be(1);

            //Current State data not changed
            response.CurrentState.CreatedAt.Should().Be(originalProcess.CurrentState.CreatedAt);
            response.CurrentState.State.Should().BeEquivalentTo(originalProcess.CurrentState.State);
            response.CurrentState.PermittedTriggers.Should().BeEquivalentTo(originalProcess.CurrentState.PermittedTriggers);

            //Rest of the object should remain the same
            response.Should().BeEquivalentTo(originalProcess, config => config.Excluding(x => x.CurrentState).Excluding(x => x.VersionNumber));
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync to update id {updatedProcessQuery.Id}", Times.Never());

            _cleanup.Add(async () => await _dynamoDb.DeleteAsync<ProcessesDb>(originalProcess.Id).ConfigureAwait(false));
        }

        [Fact]
        public async Task UpdateProcessByIdSuccessfullySavesEvenWhenAssignmentIsNull()
        {
            var originalProcess = _fixture.Build<Process>()
                                   .With(x => x.VersionNumber, (int?) null)
                                   .Create();
            await InsertDatatoDynamoDB(originalProcess.ToDatabase()).ConfigureAwait(false);

            var updateProcessRequest = _fixture.Build<UpdateProcessByIdRequestObject>().Without(x => x.Assignment).Create();

            var updatedProcessQuery = _fixture.Build<ProcessQuery>()
                        .With(x => x.Id, originalProcess.Id)
                        .Create();

            var response = await _classUnderTest.UpdateProcessById(updatedProcessQuery, updateProcessRequest, 0).ConfigureAwait(false);

            //CurrentState data changed
            response.CurrentState.ProcessData.FormData.Should().BeEquivalentTo(updateProcessRequest.FormData);
            response.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(updateProcessRequest.Documents);
            response.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            response.VersionNumber.Should().Be(1);

            //Current State data not changed
            response.CurrentState.Assignment.Should().BeEquivalentTo(originalProcess.CurrentState.Assignment);
            response.CurrentState.CreatedAt.Should().Be(originalProcess.CurrentState.CreatedAt);
            response.CurrentState.State.Should().BeEquivalentTo(originalProcess.CurrentState.State);
            response.CurrentState.PermittedTriggers.Should().BeEquivalentTo(originalProcess.CurrentState.PermittedTriggers);

            //Rest of the object should remain the same
            response.Should().BeEquivalentTo(originalProcess, config => config.Excluding(x => x.CurrentState).Excluding(x => x.VersionNumber));
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync to update id {updatedProcessQuery.Id}", Times.Never());

            _cleanup.Add(async () => await _dynamoDb.DeleteAsync<ProcessesDb>(originalProcess.Id).ConfigureAwait(false));
        }

        [Fact]
        public async Task UpdateProcessByIdThrowsVersionConflictError()
        {
            var originalProcess = _fixture.Build<Process>()
                                          .With(x => x.VersionNumber, (int?) null)
                                          .Create();
            await InsertDatatoDynamoDB(originalProcess.ToDatabase()).ConfigureAwait(false);

            var updateProcessRequest = _fixture.Create<UpdateProcessByIdRequestObject>();

            var updatedProcessQuery = _fixture.Build<ProcessQuery>()
                        .With(x => x.Id, originalProcess.Id)
                        .Create();
            var suppliedVersion = 1;

            Func<Task<Process>> func = async () => await _classUnderTest.UpdateProcessById(updatedProcessQuery, updateProcessRequest, suppliedVersion).ConfigureAwait(false);

            func.Should().Throw<VersionNumberConflictException>().WithMessage($"The version number supplied ({suppliedVersion}) does not match the current value on the entity ({0}).");
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync to update id {updatedProcessQuery.Id}", Times.Never());

            _cleanup.Add(async () => await _dynamoDb.DeleteAsync<ProcessesDb>(originalProcess.Id).ConfigureAwait(false));
        }

        [Fact]
        public async Task UpdateProcessByIdReturnsNullIfProcessDoesNotExist()
        {
            var updateProcessRequest = _fixture.Create<UpdateProcessByIdRequestObject>();
            var updatedProcessQuery = _fixture.Create<ProcessQuery>();

            var response = await _classUnderTest.UpdateProcessById(updatedProcessQuery, updateProcessRequest, 0).ConfigureAwait(false);

            response.Should().BeNull();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync to update id {updatedProcessQuery.Id}", Times.Never());


        }

    }
}
