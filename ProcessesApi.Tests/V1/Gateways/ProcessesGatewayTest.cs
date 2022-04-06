using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
using FluentAssertions;
using Force.DeepCloner;
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
        private readonly Mock<IEntityUpdater> _mockUpdater;
        private const string RequestBody = "{ \"FormData\":\"key7d2d6e42-0cbf-411a-b66c-bc35da8b6061\":{ },\"Documents\":[\"89017f11-95f7-434d-96f8-178e33685fb4\"],\"Assignment\":{\"Type\":\"Type8a4da85c-5da4-43ba-a77d-08582db9f97f\",\"Value\":\"Value6543846f-1aa8-4095-b984-4ceac8c2770f\",\"Patch\":\"Patch557d8db0-2ccf-422d-9c6f-bf7d1737f5a5\"}}";




        public ProcessesGatewayTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _mockUpdater = new Mock<IEntityUpdater>();
            _logger = new Mock<ILogger<ProcessesGateway>>();
            _classUnderTest = new ProcessesGateway(_dbFixture.DynamoDbContext, _mockUpdater.Object, _logger.Object);
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
            _classUnderTest = new ProcessesGateway(mockDynamoDb.Object, _mockUpdater.Object, _logger.Object);

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

            var updatedProcess = originalProcess.DeepClone();
            updatedProcess.CurrentState.Assignment = updateProcessRequest.Assignment;
            updatedProcess.CurrentState.ProcessData.Documents = updateProcessRequest.ProcessData.Documents;
            updatedProcess.CurrentState.ProcessData.FormData = updateProcessRequest.ProcessData.FormData;
            updatedProcess.VersionNumber = 0;

            _mockUpdater.Setup(x => x.UpdateEntity(It.IsAny<ProcessState>(), RequestBody, updateProcessRequest))
                        .Returns(new UpdateEntityResult<ProcessState>()
                        {
                            UpdatedEntity = updatedProcess.CurrentState,
                            OldValues = new Dictionary<string, object>
                            {
                                { "assignment", originalProcess.CurrentState.Assignment },
                                { "formData", originalProcess.CurrentState.ProcessData.FormData},
                                { "documents", originalProcess.CurrentState.ProcessData.Documents}
                            },
                            NewValues = new Dictionary<string, object>
                            {
                               { "assignment", updatedProcess.CurrentState.Assignment },
                                { "formData", updatedProcess.CurrentState.ProcessData.FormData},
                                { "documents", updatedProcess.CurrentState.ProcessData.Documents}
                            }
                        });


            var response = await _classUnderTest.UpdateProcessById(updatedProcessQuery, updateProcessRequest, RequestBody, 0).ConfigureAwait(false);

            var load = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(originalProcess.Id).ConfigureAwait(false);

            //CurrentState data changed
            load.CurrentState.ProcessData.FormData.Should().BeEquivalentTo(updateProcessRequest.ProcessData.FormData);
            load.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(updateProcessRequest.ProcessData.Documents);
            load.CurrentState.Assignment.Should().BeEquivalentTo(updateProcessRequest.Assignment);
            //load.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            load.VersionNumber.Should().Be(1);

            //Current State data not changed
            load.CurrentState.CreatedAt.Should().Be(originalProcess.CurrentState.CreatedAt);
            load.CurrentState.State.Should().BeEquivalentTo(originalProcess.CurrentState.State);
            load.CurrentState.PermittedTriggers.Should().BeEquivalentTo(originalProcess.CurrentState.PermittedTriggers);

            //Rest of the object should remain the same
            load.Should().BeEquivalentTo(originalProcess, config => config.Excluding(x => x.CurrentState).Excluding(x => x.VersionNumber));
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync to update id {updatedProcessQuery.Id}", Times.Once());

            //OldValues should be same as OrignalProcess
            response.OldValues["assignment"].Should().BeEquivalentTo(originalProcess.ToDatabase().CurrentState.Assignment);
            response.OldValues["formData"].Should().BeEquivalentTo(originalProcess.ToDatabase().CurrentState.ProcessData.FormData);
            response.OldValues["documents"].Should().BeEquivalentTo(originalProcess.ToDatabase().CurrentState.ProcessData.Documents);

            //NewValues should be same as UpdatedProcess
            response.NewValues["assignment"].Should().BeEquivalentTo(response.UpdatedEntity.Assignment);
            response.NewValues["formData"].Should().BeEquivalentTo(updatedProcess.CurrentState.ProcessData.FormData);
            response.NewValues["documents"].Should().BeEquivalentTo(response.UpdatedEntity.ProcessData.Documents);

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

            var updatedProcess = originalProcess.DeepClone();
            updatedProcess.CurrentState.ProcessData.Documents = updateProcessRequest.ProcessData.Documents;
            updatedProcess.CurrentState.ProcessData.FormData = updateProcessRequest.ProcessData.FormData;
            updatedProcess.VersionNumber = 0;

            _mockUpdater.Setup(x => x.UpdateEntity(It.IsAny<ProcessState>(), RequestBody, updateProcessRequest))
                        .Returns(new UpdateEntityResult<ProcessState>()
                        {
                            UpdatedEntity = updatedProcess.CurrentState,
                            OldValues = new Dictionary<string, object>
                            {
                                { "formData", originalProcess.CurrentState.ProcessData.FormData},
                                { "documents", originalProcess.CurrentState.ProcessData.Documents}
                            },
                            NewValues = new Dictionary<string, object>
                            {
                                { "formData", updatedProcess.CurrentState.ProcessData.FormData},
                                { "documents", updatedProcess.CurrentState.ProcessData.Documents}
                            }
                        });

            var response = await _classUnderTest.UpdateProcessById(updatedProcessQuery, updateProcessRequest, RequestBody, 0).ConfigureAwait(false);

            var load = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(originalProcess.Id).ConfigureAwait(false);

            //CurrentState data changed
            load.CurrentState.ProcessData.FormData.Should().BeEquivalentTo(updateProcessRequest.ProcessData.FormData);
            load.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(updateProcessRequest.ProcessData.Documents);
            load.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            load.VersionNumber.Should().Be(1);

            //Current State data not changed
            load.CurrentState.Assignment.Should().BeEquivalentTo(originalProcess.CurrentState.Assignment);
            load.CurrentState.CreatedAt.Should().Be(originalProcess.CurrentState.CreatedAt);
            load.CurrentState.State.Should().BeEquivalentTo(originalProcess.CurrentState.State);
            load.CurrentState.PermittedTriggers.Should().BeEquivalentTo(originalProcess.CurrentState.PermittedTriggers);

            //Rest of the object should remain the same
            load.Should().BeEquivalentTo(originalProcess, config => config.Excluding(x => x.CurrentState).Excluding(x => x.VersionNumber));
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync to update id {updatedProcessQuery.Id}", Times.Once());

            //OldValues should be same as OrignalProcess
            response.OldValues["formData"].Should().BeEquivalentTo(originalProcess.ToDatabase().CurrentState.ProcessData.FormData);
            response.OldValues["documents"].Should().BeEquivalentTo(originalProcess.ToDatabase().CurrentState.ProcessData.Documents);

            //NewValues should be same as UpdatedProcess
            response.NewValues["formData"].Should().BeEquivalentTo(updatedProcess.CurrentState.ProcessData.FormData);
            response.NewValues["documents"].Should().BeEquivalentTo(response.UpdatedEntity.ProcessData.Documents);

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

            Func<Task<UpdateEntityResult<ProcessState>>> func = async () => await _classUnderTest.UpdateProcessById(updatedProcessQuery, updateProcessRequest, RequestBody, suppliedVersion).ConfigureAwait(false);

            func.Should().Throw<VersionNumberConflictException>().WithMessage($"The version number supplied ({suppliedVersion}) does not match the current value on the entity ({0}).");
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync to update id {updatedProcessQuery.Id}", Times.Never());

            _cleanup.Add(async () => await _dynamoDb.DeleteAsync<ProcessesDb>(originalProcess.Id).ConfigureAwait(false));
        }

        [Fact]
        public async Task UpdateProcessByIdReturnsNullIfProcessDoesNotExist()
        {
            var updateProcessRequest = _fixture.Create<UpdateProcessByIdRequestObject>();
            var updatedProcessQuery = _fixture.Create<ProcessQuery>();

            var response = await _classUnderTest.UpdateProcessById(updatedProcessQuery, updateProcessRequest, RequestBody, 0).ConfigureAwait(false);

            response.Should().BeNull();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync to update id {updatedProcessQuery.Id}", Times.Never());


        }

    }
}
