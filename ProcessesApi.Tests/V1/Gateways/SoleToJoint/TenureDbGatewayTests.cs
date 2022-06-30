using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Shared;
using Hackney.Shared.Tenure.Infrastructure;
using Hackney.Shared.Tenure.Factories;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Gateways;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Boundary.Requests;
using Hackney.Shared.Person;
using System.Linq;
using ProcessesApi.V1.Infrastructure;

namespace ProcessesApi.Tests.V1.Gateways
{
    [Collection("AppTest collection")]
    public class TenureDbGatewayTests : IDisposable
    {
        private readonly Fixture _fixture = new Fixture();
        private readonly IDynamoDbFixture _dbFixture;
        private IDynamoDBContext _dynamoDb => _dbFixture.DynamoDbContext;

        private EntityUpdater _entityUpdater;
        private TenureDbGateway _classUnderTest;
        private readonly Mock<ILogger<TenureDbGateway>> _logger;
        private readonly List<Action> _cleanup = new List<Action>();


        public TenureDbGatewayTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _logger = new Mock<ILogger<TenureDbGateway>>();
            var entityUpdaterlogger = new Mock<ILogger<EntityUpdater>>();
            _entityUpdater = new EntityUpdater(entityUpdaterlogger.Object);
            _classUnderTest = new TenureDbGateway(_dbFixture.DynamoDbContext, _logger.Object, _entityUpdater);
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

        private async Task InsertDatatoDynamoDB(TenureInformationDb entity)
        {
            await _dbFixture.SaveEntityAsync(entity).ConfigureAwait(false);

        }

        [Fact]
        public async Task GetTenureByIdReturnsNullIfEntityDoesntExist()
        {
            // Arrange
            var id = Guid.NewGuid();
            // Act
            var response = await _classUnderTest.GetTenureById(id).ConfigureAwait(false);
            // Assert
            response.Should().BeNull();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {id}", Times.Once());
        }

        [Fact]
        public async Task GetTenureByIdReturnsTheTenureIfItExists()
        {
            // Arrange
            var entity = _fixture.Build<TenureInformation>()
                                 .With(x => x.EndOfTenureDate, DateTime.UtcNow)
                                 .With(x => x.StartOfTenureDate, DateTime.UtcNow)
                                 .With(x => x.SuccessionDate, DateTime.UtcNow)
                                 .With(x => x.PotentialEndDate, DateTime.UtcNow)
                                 .With(x => x.SubletEndDate, DateTime.UtcNow)
                                 .With(x => x.EvictionDate, DateTime.UtcNow)
                                 .With(x => x.VersionNumber, (int?) null)
                                 .Create();
            await InsertDatatoDynamoDB(entity.ToDatabase()).ConfigureAwait(false);
            // Act
            var response = await _classUnderTest.GetTenureById(entity.Id).ConfigureAwait(false);
            // Assert
            response.Should().BeEquivalentTo(entity, config => config.Excluding(y => y.VersionNumber));
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {entity.Id}", Times.Once());
        }

        [Fact]
        public async Task UpdateTenureByIdReturnsUpdatesTenureEndDate()
        {
            // Arrange
            var entity = _fixture.Build<TenureInformation>()
                                 .Without(x => x.EndOfTenureDate)
                                 .With(x => x.StartOfTenureDate, DateTime.UtcNow)
                                 .With(x => x.SuccessionDate, DateTime.UtcNow)
                                 .With(x => x.PotentialEndDate, DateTime.UtcNow)
                                 .With(x => x.SubletEndDate, DateTime.UtcNow)
                                 .With(x => x.EvictionDate, DateTime.UtcNow)
                                 .With(x => x.VersionNumber, (int?) null)
                                 .Create();

            await InsertDatatoDynamoDB(entity.ToDatabase()).ConfigureAwait(false);

            var request = new EditTenureDetailsRequestObject()
            {
                StartOfTenureDate = entity.StartOfTenureDate,
                EndOfTenureDate = DateTime.UtcNow,
                TenureType = entity.TenureType
            };

            var response = await _classUnderTest.UpdateTenureById(entity.Id, request).ConfigureAwait(false);
            // Assert
            response.NewValues.Should().ContainKey("endOfTenureDate");
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync to update tenure id {entity.Id}", Times.Once());
        }

        [Fact]
        public async Task CreateTenureReturnsNewTenure()
        {
            // Arrange
            var person = _fixture.Build<Person>()
                                 .With(x => x.VersionNumber, (int?) null)
                                 .Create();

            var householdMemberList = new List<HouseholdMembers>();
            var householdMember = new HouseholdMembers()
            {
                Id = Guid.NewGuid(),
                DateOfBirth = (DateTime) person.DateOfBirth,
                FullName = $"{person.FirstName} {person.Surname}",
                IsResponsible = true,
                PersonTenureType = (PersonTenureType) person.PersonTypes.FirstOrDefault(),
                Type = HouseholdMembersType.Person

            };
            householdMemberList.Add(householdMember);
            var tenureDetails = person.Tenures.FirstOrDefault();
            var tenuredAsset = new TenuredAsset()
            {
                Id = tenureDetails.Id,
                FullAddress = tenureDetails.AssetFullAddress,
                PropertyReference = tenureDetails.PropertyReference,
                Uprn = tenureDetails.Uprn
            };
            var createTenure = _fixture.Build<CreateTenureRequestObject>()
                                 .With(x => x.StartOfTenureDate, DateTime.UtcNow)
                                 .With(x => x.HouseholdMembers, householdMemberList)
                                 .With(x => x.TenuredAsset, tenuredAsset)
                                 .With(x => x.PaymentReference, tenureDetails.PaymentReference)
                                 .With(x => x.SuccessionDate, DateTime.UtcNow)
                                 .With(x => x.PotentialEndDate, DateTime.UtcNow)
                                 .With(x => x.SubletEndDate, DateTime.UtcNow)
                                 .With(x => x.EvictionDate, DateTime.UtcNow)
                                 .Without(x => x.EndOfTenureDate)
                                 .Create();

            // Act
            var response = await _classUnderTest.PostNewTenureAsync(createTenure).ConfigureAwait(false);
            // Assert
            var DbEntity = createTenure.ToDatabase();
            response.Should().BeEquivalentTo(DbEntity, config => config.Excluding(x => x.VersionNumber));

            _cleanup.Add(async () => await _dbFixture.DynamoDbContext.DeleteAsync(createTenure).ConfigureAwait(false));
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.SaveAsync", Times.Once());
        }

        [Fact]
        public void GetTenureByIdExceptionIsThrown()
        {
            // Arrange
            var mockDynamoDb = new Mock<IDynamoDBContext>();
            _classUnderTest = new TenureDbGateway(mockDynamoDb.Object, _logger.Object, _entityUpdater);

            var id = Guid.NewGuid();
            var exception = new ApplicationException("Test Exception");

            mockDynamoDb.Setup(x => x.LoadAsync<TenureInformationDb>(id, default))
                     .ThrowsAsync(exception);
            // Act
            Func<Task<TenureInformation>> func = async () => await _classUnderTest.GetTenureById(id).ConfigureAwait(false);
            // Assert
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
            mockDynamoDb.Verify(x => x.LoadAsync<TenureInformationDb>(id, default), Times.Once);
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {id}", Times.Once());
        }

    }
}
