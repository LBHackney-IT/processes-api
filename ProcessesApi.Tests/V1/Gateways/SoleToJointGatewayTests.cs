using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
using FluentAssertions;
using Hackney.Core.Http;
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
using ProcessesApi.V1.Domain.Finance;
using ProcessesApi.V1.Gateways;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProcessesApi.Tests.V1.Gateways
{

    [Collection("AppTest collection")]
    public class SoleToJointGatewayTests : IDisposable
    {
        private readonly Fixture _fixture = new Fixture();
        private readonly IDynamoDbFixture _dbFixture;
        private IDynamoDBContext _dynamoDb => _dbFixture.DynamoDbContext;
        private SoleToJointGateway _classUnderTest;
        private readonly List<Action> _cleanup = new List<Action>();
        private readonly Mock<ILogger<SoleToJointGateway>> _logger;
        private readonly Mock<IApiGateway> _mockApiGateway;

        private static readonly Guid _proposedTenantExistingTenureId = Guid.NewGuid();
        private static readonly Guid _correlationId = Guid.NewGuid();
        private const string IncomeApiRoute = "https://some-domain.com/api";
        private const string IncomeApiToken = "dksfghjskueygfakseygfaskjgfsdjkgfdkjsgfdkjgf";

        private const string ApiName = "Income";
        private const string IncomeApiUrlKey = "IncomeApiUrl";
        private const string IncomeApiTokenKey = "IncomeApiToken";
        private static string Route => $"{IncomeApiRoute}/agreements/{_proposedTenantExistingTenureId}";

        public SoleToJointGatewayTests(MockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;

            _logger = new Mock<ILogger<SoleToJointGateway>>();

            _mockApiGateway = new Mock<IApiGateway>();
            _mockApiGateway.SetupGet(x => x.ApiName).Returns(ApiName);
            _mockApiGateway.SetupGet(x => x.ApiRoute).Returns(IncomeApiRoute);
            _mockApiGateway.SetupGet(x => x.ApiToken).Returns(IncomeApiToken);

            _classUnderTest = new SoleToJointGateway(_dynamoDb, _logger.Object, _mockApiGateway.Object);
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

        [Fact]
        public void ConstructorTestInitialisesApiGateway()
        {
            _mockApiGateway.Verify(x => x.Initialise(ApiName, IncomeApiUrlKey, IncomeApiTokenKey, null),
                                   Times.Once);
        }

        private async Task<(Person, TenureInformation)> CreateEligibleTenureAndProposedTenant(bool proposedTenantHasExistingActiveTenures = false)
        {
            var proposedTenantId = Guid.NewGuid();
            var proposedTenantExistingTenure = _fixture.Build<TenureInformation>()
                        .With(x => x.Id, _proposedTenantExistingTenureId)
                        .With(x => x.TenureType, TenureTypes.Secure)
                        .With(x => x.EndOfTenureDate, DateTime.Now.AddDays(proposedTenantHasExistingActiveTenures ? 10 : -10))
                        .With(x => x.HouseholdMembers,
                                    new List<HouseholdMembers> {
                                        _fixture.Build<HouseholdMembers>()
                                        .With(x => x.Id, proposedTenantId)
                                        .Create()
                                    })
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();
            await _dynamoDb.SaveAsync(proposedTenantExistingTenure.ToDatabase()).ConfigureAwait(false);
            _cleanup.Add(() => _dynamoDb.DeleteAsync<TenureInformationDb>(proposedTenantExistingTenure.Id, new DynamoDBContextConfig { SkipVersionCheck = true }));

            var proposedTenant = _fixture.Build<Person>()
                                         .With(x => x.VersionNumber, (int?) null)
                                         .With(x => x.Id, proposedTenantId)
                                         .With(x => x.Tenures, new List<TenureDetails>
                                         {
                                             _fixture.Build<TenureDetails>()
                                                .With(x => x.Id, proposedTenantExistingTenure.Id)
                                                .With(x => x.EndDate, proposedTenantExistingTenure.EndOfTenureDate.ToString())
                                                .Create()
                                         })
                                         .Create();

            var tenure = _fixture.Build<TenureInformation>()
                        .With(x => x.HouseholdMembers,
                                new List<HouseholdMembers> {
                                    _fixture.Build<HouseholdMembers>()
                                    .With(x => x.Id, proposedTenantId)
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

        private async Task<bool> SaveAndCheckEligibility(TenureInformation tenure, Person proposedTenant)
        {
            await _dbFixture.SaveEntityAsync(tenure.ToDatabase()).ConfigureAwait(false);
            await _dbFixture.SaveEntityAsync(proposedTenant.ToDatabase()).ConfigureAwait(false);
            // Act
            var response = await _classUnderTest.CheckEligibility(tenure.Id, proposedTenant.Id).ConfigureAwait(false);
            return response;
        }

        [Fact]
        public async Task CheckEligibiltyReturnsTrueIfAllConditionsAreMet()
        {
            // Arrange
            (var proposedTenant, var tenure) = await CreateEligibleTenureAndProposedTenant().ConfigureAwait(false);
            var response = await SaveAndCheckEligibility(tenure, proposedTenant).ConfigureAwait(false);
            // Assert
            response.Should().BeTrue();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.Once());
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfProposedTenantIsNotANamedTenureHolderOfTheSelectedTenure()
        {
            // Arrange
            (var proposedTenant, var tenure) = await CreateEligibleTenureAndProposedTenant().ConfigureAwait(false);
            tenure.HouseholdMembers.ToListOrEmpty().Find(x => x.Id == proposedTenant.Id).PersonTenureType = PersonTenureType.Occupant;
            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.AtLeastOnce());
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheSelectedTenureIsNotCurrentlyActive()
        {
            // Arrange
            (var proposedTenant, var tenure) = await CreateEligibleTenureAndProposedTenant().ConfigureAwait(false);
            tenure.EndOfTenureDate = DateTime.Now.AddDays(-10);
            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.AtLeastOnce());
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheSelectedTenureIsNotSecure()
        {
            // Arrange
            (var proposedTenant, var tenure) = await CreateEligibleTenureAndProposedTenant().ConfigureAwait(false);
            tenure.TenureType = TenureTypes.NonSecure;
            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.AtLeastOnce());
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheProposedTenantIsAMinor()
        {
            // Arrange
            (var proposedTenant, var tenure) = await CreateEligibleTenureAndProposedTenant().ConfigureAwait(false);
            tenure.HouseholdMembers.ToListOrEmpty()
                                            .Find(x => x.Id == proposedTenant.Id)
                                            .DateOfBirth = DateTime.Now;
            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.AtLeastOnce());
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheProposedTenantIsAHouseholdMemberOfAnExistingNonSecureTenure()
        {
            // Arrange
            (var proposedTenant, var tenure) = await CreateEligibleTenureAndProposedTenant().ConfigureAwait(false);

            var nonSecureTenureDetails = _fixture.Create<TenureDetails>();
            var updatedTenures = proposedTenant.Tenures.ToListOrEmpty();
            updatedTenures.Add(nonSecureTenureDetails);
            proposedTenant.Tenures = updatedTenures;

            var nonSecureTenure = _fixture.Build<TenureInformation>()
                .With(x => x.VersionNumber, (int?) null)
                .With(x => x.Id, nonSecureTenureDetails.Id)
                .With(x => x.TenureType, TenureTypes.NonSecure)
                .With(x => x.HouseholdMembers, new List<HouseholdMembers>
                                                {
                                                    _fixture.Build<HouseholdMembers>()
                                                    .With(x => x.Id, proposedTenant.Id)
                                                    .Create()
                                                })
                .Create();
            await _dbFixture.SaveEntityAsync(nonSecureTenure.ToDatabase()).ConfigureAwait(false);
            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.AtLeastOnce());
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Person ID: {proposedTenant.Id}", Times.Once());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task CheckEligibiltyFailsIfTheProposedTenantIsResponsibleInAnActiveJointTenancy(bool isResponsible)
        {
            // Arrange
            (var proposedTenant, var tenure) = await CreateEligibleTenureAndProposedTenant().ConfigureAwait(false);

            var jointTenureDetails = _fixture.Build<TenureDetails>()
                                             .With(x => x.EndDate, DateTime.Now.AddYears(10).ToString())
                                             .Create();
            var updatedTenures = proposedTenant.Tenures.ToListOrEmpty();
            updatedTenures.Add(jointTenureDetails);
            proposedTenant.Tenures = updatedTenures;

            var householdMembers = new List<HouseholdMembers>{
                                                                _fixture.Build<HouseholdMembers>()
                                                                       .With(x => x.IsResponsible, true)
                                                                       .Create(),
                                                                _fixture.Build<HouseholdMembers>()
                                                                       .With(x => x.IsResponsible, true)
                                                                       .Create(),
                                                                _fixture.Build<HouseholdMembers>()
                                                                        .With(x => x.Id, proposedTenant.Id)
                                                                        .With(x => x.IsResponsible, isResponsible)
                                                                        .Create()
                                                            };

            var jointTenure = _fixture.Build<TenureInformation>()
                .With(x => x.VersionNumber, (int?) null)
                .With(x => x.Id, jointTenureDetails.Id)
                .With(x => x.HouseholdMembers, householdMembers)
                .With(x => x.TenureType, TenureTypes.Secure)
                .Create();

            await _dbFixture.SaveEntityAsync(jointTenure.ToDatabase()).ConfigureAwait(false);
            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant).ConfigureAwait(false);
            // Assert
            response.Should().Be(!isResponsible);
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.AtLeastOnce());
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Person ID: {proposedTenant.Id}", Times.Once());
        }

        [Fact]
        public async Task SaveAndCheckEligibilityFailsIfTenantHasLivePaymentAgreements()
        {
            (var proposedTenant, var tenure) = await CreateEligibleTenureAndProposedTenant().ConfigureAwait(false);

            var tenureWithArrears = (proposedTenant.Tenures.ToListOrEmpty()).FirstOrDefault();
            tenureWithArrears.EndDate = DateTime.Now.AddDays(10).ToString();
            var paymentAgreement = _fixture.Build<PaymentAgreement>()
                                           .With(x => x.TenancyRef, tenureWithArrears.Id.ToString())
                                           .With(x => x.CurrentState, "live")
                                           .Create();

            _mockApiGateway.Setup(x => x.GetByIdAsync<PaymentAgreement>(Route, _proposedTenantExistingTenureId, It.IsAny<Guid>())).ReturnsAsync(paymentAgreement);

            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _mockApiGateway.Verify(x => x.GetByIdAsync<PaymentAgreement>(Route, _proposedTenantExistingTenureId, It.IsAny<Guid>()), Times.Once);
            _logger.VerifyExact(LogLevel.Debug, $"Calling Income API for payment agreeement with Tenure ID: {_proposedTenantExistingTenureId}", Times.AtLeastOnce());
        }
    }
}
