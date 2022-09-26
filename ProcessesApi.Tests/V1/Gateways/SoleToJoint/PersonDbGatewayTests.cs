using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Shared;
using Hackney.Shared.Person.Infrastructure;
using Hackney.Shared.Person.Factories;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessesApi.V1.Gateways;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Hackney.Shared.Person;
using Hackney.Shared.Processes.Infrastructure;
using Hackney.Shared.Person.Boundary.Request;

namespace ProcessesApi.Tests.V1.Gateways
{
    [Collection("AppTest collection")]
    public class PersonDbGatewayTests : IDisposable
    {
        private readonly Fixture _fixture = new Fixture();
        private readonly IDynamoDbFixture _dbFixture;
        private readonly EntityUpdater _updater;
        private IDynamoDBContext _dynamoDb => _dbFixture.DynamoDbContext;
        private PersonDbGateway _classUnderTest;
        private readonly Mock<ILogger<PersonDbGateway>> _logger;
        private readonly List<Action> _cleanup = new List<Action>();

        public PersonDbGatewayTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _logger = new Mock<ILogger<PersonDbGateway>>();
            var entityUpdaterlogger = new Mock<ILogger<EntityUpdater>>();
            _updater = new EntityUpdater(entityUpdaterlogger.Object);
            _classUnderTest = new PersonDbGateway(_dbFixture.DynamoDbContext, _logger.Object, _updater);
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

        private async Task InsertDatatoDynamoDB(PersonDbEntity entity)
        {
            await _dbFixture.SaveEntityAsync(entity).ConfigureAwait(false);
        }

        [Fact]
        public async Task GetPersonByIdReturnsNullIfEntityDoesntExist()
        {
            // Arrange
            var id = Guid.NewGuid();
            // Act
            var response = await _classUnderTest.GetPersonById(id).ConfigureAwait(false);
            // Assert
            response.Should().BeNull();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Person ID: {id}", Times.Once());
        }

        [Fact]
        public async Task GetPersonByIdReturnsThePersonIfItExists()
        {
            // Arrange
            var entity = _fixture.Build<Person>()
                        .With(x => x.VersionNumber, (int?) null)
                        .With(x => x.DateOfBirth, DateTime.UtcNow)
                        .Create();
            await InsertDatatoDynamoDB(entity.ToDatabase()).ConfigureAwait(false);
            // Act
            var response = await _classUnderTest.GetPersonById(entity.Id).ConfigureAwait(false);
            // Assert
            response.Should().BeEquivalentTo(entity, config => config.Excluding(y => y.VersionNumber).Excluding(x => x.LastModified));
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Person ID: {entity.Id}", Times.Once());
        }

        [Fact]
        public void GetPersonByIdExceptionIsThrown()
        {
            // Arrange
            var mockDynamoDb = new Mock<IDynamoDBContext>();
            _classUnderTest = new PersonDbGateway(mockDynamoDb.Object, _logger.Object, _updater);

            var id = Guid.NewGuid();
            var exception = new ApplicationException("Test Exception");

            mockDynamoDb.Setup(x => x.LoadAsync<PersonDbEntity>(id, default))
                     .ThrowsAsync(exception);
            // Act
            Func<Task<Person>> func = async () => await _classUnderTest.GetPersonById(id).ConfigureAwait(false);
            // Assert
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
            mockDynamoDb.Verify(x => x.LoadAsync<PersonDbEntity>(id, default), Times.Once);
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Person ID: {id}", Times.Once());
        }

        [Fact]
        public async Task UpdatePersonByIdReturnsUpdatesPersonName()
        {
            // Arrange
            var entity = _fixture.Build<Person>()
                                 .With(x => x.VersionNumber, (int?) null)
                                 .Create();

            await InsertDatatoDynamoDB(entity.ToDatabase()).ConfigureAwait(false);

            var request = new UpdatePersonRequestObject()
            {
                FirstName = _fixture.Create<string>()
            };

            var response = await _classUnderTest.UpdatePersonByIdAsync(entity.Id, request).ConfigureAwait(false);
            // Assert
            var updatePerson = await _classUnderTest.GetPersonById(entity.Id).ConfigureAwait(false);
            response.NewValues.Should().ContainKey("firstName");
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync to update person id {entity.Id}", Times.Once());
        }

    }
}
