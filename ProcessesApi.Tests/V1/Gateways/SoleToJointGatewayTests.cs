using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Shared;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Factories;
using Hackney.Shared.Tenure.Infrastructure;
using Microsoft.Extensions.Logging;
using Moq;
using ProcessesApi.V1.Gateways;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ProcessesApi.Tests.V1.Gateways
{
    public class EligibilityFailureTestCase
    {
        public string Name { get; set; }
        public Func<TenureInformation, Guid, TenureInformation> Function { get; set; }

        public override string ToString()
        {
            return Name.ToString();
        }
    }

    [Collection("AppTest collection")]
    public class SoleToJointGatewayTests : IDisposable
    {
        private readonly Fixture _fixture = new Fixture();
        private readonly IDynamoDbFixture _dbFixture;
        private IDynamoDBContext _dynamoDb => _dbFixture.DynamoDbContext;
        private SoleToJointGateway _classUnderTest;
        private readonly List<Action> _cleanup = new List<Action>();
        private readonly Mock<ILogger<SoleToJointGateway>> _logger;


        public SoleToJointGatewayTests(MockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _logger = new Mock<ILogger<SoleToJointGateway>>();
            _classUnderTest = new SoleToJointGateway(_dynamoDb, _logger.Object);
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

        private (Guid, TenureInformation) CreateEligibleTenure()
        {
            var incomingTenantId = Guid.NewGuid();
            var processTenure = _fixture.Build<TenureInformation>()
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
            return (incomingTenantId, processTenure);
        }

        [Fact]
        public async Task CheckEligibiltyReturnsTrueIfAllConditionsAreMet()
        {
            // Arrange
            (var incomingTenantId, var processTenure) = CreateEligibleTenure();
            await InsertDatatoDynamoDB(processTenure.ToDatabase()).ConfigureAwait(false);
            // Act
            var response = await _classUnderTest.CheckEligibility(processTenure.Id, incomingTenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeTrue();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {processTenure.Id}", Times.Once());
        }

        private async Task ShouldNotBeEligible(TenureInformation tenure, Guid tenantId)
        {
            await InsertDatatoDynamoDB(tenure.ToDatabase()).ConfigureAwait(false);
            // Act
            var response = await _classUnderTest.CheckEligibility(tenure.Id, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.Once());
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfIncomingTenantIsNotANamedTenureHolder()
        {
            // Arrange
            (var incomingTenantId, var tenure) = CreateEligibleTenure();
            tenure.HouseholdMembers.ToListOrEmpty().Find(x => x.Id == incomingTenantId).PersonTenureType = PersonTenureType.Occupant;
            // Act & assert
            await ShouldNotBeEligible(tenure, incomingTenantId).ConfigureAwait(false);
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheTenureHasMoreThanOneResponsibleMember()
        {
            // Arrange
            (var incomingTenantId, var tenure) = CreateEligibleTenure();
            var householdMembers = tenure.HouseholdMembers.ToListOrEmpty();
            householdMembers.Add(_fixture.Build<HouseholdMembers>()
                                            .With(x => x.IsResponsible, true)
                                            .Create());
            tenure.HouseholdMembers = householdMembers;
            // Act & assert
            await ShouldNotBeEligible(tenure, incomingTenantId).ConfigureAwait(false);
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheTenureIsNotCurrentlyActive()
        {
            // Arrange
            (var incomingTenantId, var tenure) = CreateEligibleTenure();
            tenure.EndOfTenureDate = DateTime.Now.AddDays(-10);
            // Act & assert
            await ShouldNotBeEligible(tenure, incomingTenantId).ConfigureAwait(false);
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheTenureIsNotSecure()
        {
            // Arrange
            (var incomingTenantId, var tenure) = CreateEligibleTenure();
            tenure.TenureType = TenureTypes.NonSecure;
            // Act & assert
            await ShouldNotBeEligible(tenure, incomingTenantId).ConfigureAwait(false);
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheIncomingTenantIsAMinor()
        {
            // Arrange
            (var incomingTenantId, var tenure) = CreateEligibleTenure();
            tenure.HouseholdMembers.ToListOrEmpty()
                                            .Find(x => x.Id == incomingTenantId)
                                            .DateOfBirth = DateTime.Now;
            // Act & assert
            await ShouldNotBeEligible(tenure, incomingTenantId).ConfigureAwait(false);
        }
    }
}
