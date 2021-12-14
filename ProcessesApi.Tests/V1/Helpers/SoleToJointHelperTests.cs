using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Shared;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessesApi.V1.Helpers;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ProcessesApi.Tests.V1.Helpers
{
    [Collection("AppTest collection")]
    public class SoleToJointHelperTests : IDisposable
    {
        private readonly Fixture _fixture = new Fixture();
        private readonly IDynamoDbFixture _dbFixture;
        private IDynamoDBContext _dynamoDb => _dbFixture.DynamoDbContext;
        private SoleToJointHelper _classUnderTest;
        private readonly List<Action> _cleanup = new List<Action>();
        private readonly Mock<ILogger<SoleToJointHelper>> _logger;


        public SoleToJointHelperTests(MockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _logger = new Mock<ILogger<SoleToJointHelper>>();
            _classUnderTest = new SoleToJointHelper(_dynamoDb, _logger.Object);
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
        public async Task CheckEligibiltyReturnsTrueIfAllConditionsAreMet()
        {
            // Arrange
            var incomingTenantId = Guid.NewGuid();
            var processTenure = _fixture.Build<TenureInformationDb>()
                                .With(x => x.HouseholdMembers,
                                      new List<HouseholdMembers> { 
                                          _fixture.Build<HouseholdMembers>()
                                            .With(x => x.Id, incomingTenantId)
                                            .With(x => x.PersonTenureType, PersonTenureType.Tenant)
                                            .With(x => x.IsResponsible, true)
                                            .With(x => x.DateOfBirth, DateTime.Now.AddYears(-18))
                                            .Create()
                                      })
                                .With(x => x.TenureType, TenureTypes.Secure)
                                .With(x => x.EndOfTenureDate, (DateTime?) null)
                                .With(x => x.VersionNumber, (int?) null)
                                .Create();
            await InsertDatatoDynamoDB(processTenure).ConfigureAwait(false);
            // Act
            var response = await _classUnderTest.CheckEligibility(processTenure.Id, incomingTenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeTrue();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {processTenure.Id}", Times.Once());
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfIncomingTenantIsNotATenant()
        {
            // Arrange
            var incomingTenantId = Guid.NewGuid();
            var processTenure = _fixture.Build<TenureInformationDb>()
                                .With(x => x.HouseholdMembers,
                                      new List<HouseholdMembers> { 
                                          _fixture.Build<HouseholdMembers>()
                                            .With(x => x.Id, incomingTenantId)
                                            .With(x => x.PersonTenureType, PersonTenureType.Occupant)
                                            .With(x => x.IsResponsible, true)
                                            .With(x => x.DateOfBirth, DateTime.Now.AddYears(-18))
                                            .Create()
                                      })
                                .With(x => x.TenureType, TenureTypes.Secure)
                                .With(x => x.EndOfTenureDate, (DateTime?) null)
                                .With(x => x.VersionNumber, (int?) null)
                                .Create();
            await InsertDatatoDynamoDB(processTenure).ConfigureAwait(false);
            // Act
            var response = await _classUnderTest.CheckEligibility(processTenure.Id, incomingTenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {processTenure.Id}", Times.Once());
        }
    }
}
