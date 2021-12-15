using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Shared;
using Hackney.Shared.Person;
using Hackney.Shared.Person.Domain;
using Hackney.Shared.Person.Factories;
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

        private (Person, TenureInformation) CreateEligibleTenureAndProposedTenant()
        {
            var proposedTenant = _fixture.Build<Person>()
                                         .With(x => x.VersionNumber, (int?) null)
                                         .With(x => x.Tenures, new List<TenureDetails>())
                                         .Create();
            var tenure = _fixture.Build<TenureInformation>()
                        .With(x => x.HouseholdMembers,
                                new List<HouseholdMembers> {
                                    _fixture.Build<HouseholdMembers>()
                                    .With(x => x.Id, proposedTenant.Id)
                                    .With(x => x.PersonTenureType, PersonTenureType.Tenant)
                                    .With(x => x.DateOfBirth, DateTime.Now.AddYears(-18))
                                    .Create()
                                })
                        .With(x => x.TenureType, TenureTypes.Secure)
                        .With(x => x.EndOfTenureDate, (DateTime?) null)
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();
            return (proposedTenant, tenure);
        }

        [Fact]
        public async Task CheckEligibiltyReturnsTrueIfAllConditionsAreMet()
        {
            // Arrange
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();
            await _dbFixture.SaveEntityAsync(tenure.ToDatabase()).ConfigureAwait(false);
            await _dbFixture.SaveEntityAsync(proposedTenant.ToDatabase()).ConfigureAwait(false);
            // Act
            var response = await _classUnderTest.CheckEligibility(tenure.Id, proposedTenant.Id).ConfigureAwait(false);
            // Assert
            response.Should().BeTrue();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.Once());
        }

        private async Task ShouldNotBeEligible(TenureInformation tenure, Person proposedTenant)
        {
            await _dbFixture.SaveEntityAsync(tenure.ToDatabase()).ConfigureAwait(false);
            await _dbFixture.SaveEntityAsync(proposedTenant.ToDatabase()).ConfigureAwait(false);
            // Act
            var response = await _classUnderTest.CheckEligibility(tenure.Id, proposedTenant.Id).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.AtLeastOnce());
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfProposedTenantIsNotANamedTenureHolderOfTheSelectedTenure()
        {
            // Arrange
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();
            tenure.HouseholdMembers.ToListOrEmpty().Find(x => x.Id == proposedTenant.Id).PersonTenureType = PersonTenureType.Occupant;
            // Act & assert
            await ShouldNotBeEligible(tenure, proposedTenant).ConfigureAwait(false);
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheSelectedTenureIsNotCurrentlyActive()
        {
            // Arrange
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();
            tenure.EndOfTenureDate = DateTime.Now.AddDays(-10);
            // Act & assert
            await ShouldNotBeEligible(tenure, proposedTenant).ConfigureAwait(false);
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheSelectedTenureIsNotSecure()
        {
            // Arrange
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();
            tenure.TenureType = TenureTypes.NonSecure;
            // Act & assert
            await ShouldNotBeEligible(tenure, proposedTenant).ConfigureAwait(false);
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheProposedTenantIsAMinor()
        {
            // Arrange
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();
            tenure.HouseholdMembers.ToListOrEmpty()
                                            .Find(x => x.Id == proposedTenant.Id)
                                            .DateOfBirth = DateTime.Now;
            // Act & assert
            await ShouldNotBeEligible(tenure, proposedTenant).ConfigureAwait(false);
        }

        [Fact] // TODO: Repeat test with non-active tenures & if is not responsible in joint tenure
        public async Task CheckEligibiltyReturnsFalseIfTheProposedTenantIsPartOfAJointTenancy()
        {
            // Arrange
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();

            var jointTenureDetails = _fixture.Create<TenureDetails>();
            var updatedTenures = proposedTenant.Tenures.ToListOrEmpty();
            updatedTenures.Add(jointTenureDetails);
            proposedTenant.Tenures = updatedTenures;

            var jointTenure = _fixture.Build<TenureInformation>()
                .With(x => x.VersionNumber, (int?) null)
                .With(x => x.Id, jointTenureDetails.Id)
                .With(x => x.HouseholdMembers, _fixture.Build<HouseholdMembers>()
                                                .With(x => x.IsResponsible, true)
                                                .With(x => x.Id, proposedTenant.Id)
                                                .CreateMany())
                .Create();
            await _dbFixture.SaveEntityAsync(jointTenure.ToDatabase()).ConfigureAwait(false);
            // Act & assert
            await ShouldNotBeEligible(tenure, proposedTenant).ConfigureAwait(false);
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Person ID: {proposedTenant.Id}", Times.Once());
        }

        [Fact] // TODO: Repeat test with non-active tenures
        public async Task CheckEligibiltyReturnsFalseIfTheProposedTenantIsAHouseholdMemberOfAnExistingNonSecureTenure()
        {
            // Arrange
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();

            var existingTenureDetails = _fixture.Create<TenureDetails>();
            var updatedTenures = proposedTenant.Tenures.ToListOrEmpty();
            updatedTenures.Add(existingTenureDetails);
            proposedTenant.Tenures = updatedTenures;

            var existingTenure = _fixture.Build<TenureInformation>()
                .With(x => x.VersionNumber, (int?) null)
                .With(x => x.Id, existingTenureDetails.Id)
                .With(x => x.TenureType, TenureTypes.NonSecure)
                .With(x => x.HouseholdMembers, new List<HouseholdMembers>
                                                {
                                                    _fixture.Build<HouseholdMembers>()
                                                    .With(x => x.Id, proposedTenant.Id)
                                                    .With(x => x.PersonTenureType, PersonTenureType.HouseholdMember)
                                                    .Create()
                                                })
                .Create();
            await _dbFixture.SaveEntityAsync(existingTenure.ToDatabase()).ConfigureAwait(false);
            // Act & assert
            await ShouldNotBeEligible(tenure, proposedTenant).ConfigureAwait(false);
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Person ID: {proposedTenant.Id}", Times.Once());
        }
    }
}
