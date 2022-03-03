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
using ProcessesApi.V1.Gateways.Exceptions;
using System;
using System.Collections.Generic;
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
        private static readonly String _proposedTenantExistingTenureTenancyRef = "some-uh-tag";
        private const string IncomeApiRoute = "https://some-domain.com/api";
        private const string IncomeApiToken = "dksfghjskueygfakseygfaskjgfsdjkgfdkjsgfdkjgf";

        private const string ApiName = "Income";
        private const string IncomeApiUrlKey = "IncomeApiUrl";
        private const string IncomeApiTokenKey = "IncomeApiToken";
        private static string paymentAgreementRoute => $"{IncomeApiRoute}/agreements/{_proposedTenantExistingTenureTenancyRef}";
        private static string tenanciesRoute => $"{IncomeApiRoute}/tenancies/{_proposedTenantExistingTenureTenancyRef}";

        public SoleToJointGatewayTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;

            _logger = new Mock<ILogger<SoleToJointGateway>>();

            _mockApiGateway = new Mock<IApiGateway>();
            _mockApiGateway.SetupGet(x => x.ApiName).Returns(ApiName);
            _mockApiGateway.SetupGet(x => x.ApiRoute).Returns(IncomeApiRoute);
            _mockApiGateway.SetupGet(x => x.ApiToken).Returns(IncomeApiToken);

            _classUnderTest = new SoleToJointGateway(_dbFixture.DynamoDbContext, _logger.Object, _mockApiGateway.Object);
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
            var proposedTenantId = Guid.NewGuid();

            var proposedTenant = _fixture.Build<Person>()
                                         .With(x => x.VersionNumber, (int?) null)
                                         .With(x => x.Id, proposedTenantId)
                                         .With(x => x.Tenures, new List<TenureDetails>())
                                         .Create();

            var tenure = _fixture.Build<TenureInformation>()
                        .With(x => x.HouseholdMembers,
                                new List<HouseholdMembers> {
                                    _fixture.Build<HouseholdMembers>()
                                    .With(x => x.Id, proposedTenantId)
                                    .With(x => x.PersonTenureType, PersonTenureType.Tenant)
                                    .With(x => x.DateOfBirth, DateTime.UtcNow.AddYears(-18))
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

        private TenureInformation CreateExistingActiveTenureForPerson(Guid tenantId)
        {
            return _fixture.Build<TenureInformation>()
                        .With(x => x.Id, _proposedTenantExistingTenureId)
                        .With(x => x.TenureType, TenureTypes.Secure)
                        .With(x => x.EndOfTenureDate, DateTime.Now.AddDays(10))
                        .With(x => x.HouseholdMembers,
                                    new List<HouseholdMembers> {
                                        _fixture.Build<HouseholdMembers>()
                                            .With(x => x.Id, tenantId)
                                            .With(x => x.IsResponsible, true)
                                            .Create()
                                    })
                        .With(x => x.LegacyReferences,
                                    new List<LegacyReference> {
                                        new LegacyReference
                                        {
                                            Name = "uh_tag_ref",
                                            Value = _proposedTenantExistingTenureTenancyRef
                                        }
                                    })
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();
        }

        private Person AddActiveTenureToPersonRecord(Person tenant)
        {
            var personTenures = tenant.Tenures.ToListOrEmpty();
            personTenures.Add(_fixture.Build<TenureDetails>()
                                      .With(x => x.Id, _proposedTenantExistingTenureId)
                                      .With(x => x.EndDate, DateTime.Now.AddDays(10).ToString())
                                      .Create());
            tenant.Tenures = personTenures;
            return tenant;
        }

        private TenureInformation AddHouseholdMemberToTenureRecord(TenureInformation tenure, bool isResponsible)
        {
            var householdMembers = tenure.HouseholdMembers.ToListOrEmpty();
            householdMembers.Add(_fixture.Build<HouseholdMembers>()
                                        .With(x => x.IsResponsible, isResponsible)
                                        .Create());
            tenure.HouseholdMembers = householdMembers;
            return tenure;
        }

        private async Task SaveProposedTenantExistingTenureToDatabase(TenureInformation tenure)
        {
            await _dynamoDb.SaveAsync(tenure.ToDatabase()).ConfigureAwait(false);
            _cleanup.Add(() => _dynamoDb.DeleteAsync<TenureInformationDb>(tenure.Id, new DynamoDBContextConfig { SkipVersionCheck = true }));
        }

        [Fact]
        public void ConstructorTestInitialisesApiGateway()
        {
            _mockApiGateway.Verify(x => x.Initialise(ApiName, IncomeApiUrlKey, IncomeApiTokenKey, null, true),
                                   Times.Once);
        }

        [Fact]
        public async Task CheckEligibiltyReturnsTrueIfAllConditionsAreMet()
        {
            // Arrange
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();
            var response = await SaveAndCheckEligibility(tenure, proposedTenant).ConfigureAwait(false);
            // Assert
            response.Should().BeTrue();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.Once());
        }

        [Fact]
        public async Task CheckEligibilityThrowsErrorIfTheTargetTenureIsNotFound()
        {
            // Arrange
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();
            await _dbFixture.SaveEntityAsync(proposedTenant.ToDatabase()).ConfigureAwait(false);
            // Act
            Func<Task<bool>> func = async () => await _classUnderTest.CheckEligibility(tenure.Id, proposedTenant.Id).ConfigureAwait(false);
            // Assert
            func.Should().Throw<TenureNotFoundException>().WithMessage($"Tenure with id {tenure.Id} not found.");
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.Once());
        }

        [Fact]
        public async Task CheckEligibilityThrowsErrorIfTheProposedTenantIsNotFound()
        {
            // Arrange
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();
            await _dbFixture.SaveEntityAsync(tenure.ToDatabase()).ConfigureAwait(false);
            // Act
            Func<Task<bool>> func = async () => await _classUnderTest.CheckEligibility(tenure.Id, proposedTenant.Id).ConfigureAwait(false);
            // Assert
            func.Should().Throw<PersonNotFoundException>().WithMessage($"Person with id {proposedTenant.Id} not found.");
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.Once());
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Person ID: {proposedTenant.Id}", Times.Once());
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfProposedTenantIsNotANamedTenureHolderOfTheSelectedTenure()
        {
            // Arrange
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();
            var householdMembers = tenure.HouseholdMembers.ToListOrEmpty();
            householdMembers.Find(x => x.Id == proposedTenant.Id).PersonTenureType = PersonTenureType.Occupant;
            tenure.HouseholdMembers = householdMembers;
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
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();
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
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();
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
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();

            var householdMembers = tenure.HouseholdMembers.ToListOrEmpty();
            householdMembers.Find(x => x.Id == proposedTenant.Id).DateOfBirth = DateTime.Now;
            tenure.HouseholdMembers = householdMembers;
            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.AtLeastOnce());
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheProposedTenantIsAHouseholdMemberOfAnActiveNonSecureTenure()
        {
            // Arrange
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();

            proposedTenant = AddActiveTenureToPersonRecord(proposedTenant);

            var nonSecureTenure = _fixture.Build<TenureInformation>()
                .With(x => x.VersionNumber, (int?) null)
                .With(x => x.Id, _proposedTenantExistingTenureId)
                .With(x => x.TenureType, TenureTypes.NonSecure)
                .With(x => x.HouseholdMembers, new List<HouseholdMembers>
                                                {
                                                    _fixture.Build<HouseholdMembers>()
                                                    .With(x => x.Id, proposedTenant.Id)
                                                    .Create()
                                                })
                .Create();

            await SaveProposedTenantExistingTenureToDatabase(nonSecureTenure).ConfigureAwait(false);
            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.AtLeastOnce());
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Person ID: {proposedTenant.Id}", Times.Once());
        }

        [Fact]
        public async Task CheckEligibiltyFailsIfTheProposedTenantIsResponsibleInAnActiveJointTenure()
        {
            // Arrange
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();

            var existingTenure = CreateExistingActiveTenureForPerson(proposedTenant.Id);
            existingTenure = AddHouseholdMemberToTenureRecord(existingTenure, isResponsible: true);
            await SaveProposedTenantExistingTenureToDatabase(existingTenure).ConfigureAwait(false);

            proposedTenant = AddActiveTenureToPersonRecord(proposedTenant);

            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.AtLeastOnce());
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Person ID: {proposedTenant.Id}", Times.Once());
        }

        [Fact]
        public async Task CheckEligibilityFailsIfTenantHasLivePaymentAgreements()
        {
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();

            var existingTenure = CreateExistingActiveTenureForPerson(proposedTenant.Id);
            await SaveProposedTenantExistingTenureToDatabase(existingTenure).ConfigureAwait(false);

            proposedTenant = AddActiveTenureToPersonRecord(proposedTenant);

            var paymentAgreements = new PaymentAgreements
            {
                Agreements = new List<PaymentAgreement>
                {
                    _fixture.Build<PaymentAgreement>()
                        .With(x => x.TenancyRef, _proposedTenantExistingTenureTenancyRef)
                        .With(x => x.Amount, 50)
                        .Create()
                }
            };

            _mockApiGateway.Setup(x => x.GetByIdAsync<PaymentAgreements>(paymentAgreementRoute, _proposedTenantExistingTenureTenancyRef, It.IsAny<Guid>())).ReturnsAsync(paymentAgreements);

            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _mockApiGateway.Verify(x => x.GetByIdAsync<PaymentAgreements>(paymentAgreementRoute, _proposedTenantExistingTenureTenancyRef, It.IsAny<Guid>()), Times.Once);
            _logger.VerifyExact(LogLevel.Debug, $"Calling Income API for payment agreeement with tenancy ref: {_proposedTenantExistingTenureTenancyRef}", Times.AtLeastOnce());
        }

        [Fact]
        public async Task CheckEligibilityFailsIfTenantHasAnActiveNoticeOfSeekingPossession()
        {
            (var proposedTenant, var tenure) = CreateEligibleTenureAndProposedTenant();

            var existingTenure = CreateExistingActiveTenureForPerson(proposedTenant.Id);
            await SaveProposedTenantExistingTenureToDatabase(existingTenure).ConfigureAwait(false);

            proposedTenant = AddActiveTenureToPersonRecord(proposedTenant);

            var tenancyWithNosp = _fixture.Build<Tenancy>()
                                    .With(x => x.nosp, _fixture.Build<NoticeOfSeekingPossession>()
                                                                .With(x => x.active, true)
                                                                .Create()
                                    )
                                    .Create();

            _mockApiGateway.Setup(x => x.GetByIdAsync<Tenancy>(tenanciesRoute, _proposedTenantExistingTenureTenancyRef, It.IsAny<Guid>())).ReturnsAsync(tenancyWithNosp);

            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _mockApiGateway.Verify(x => x.GetByIdAsync<Tenancy>(tenanciesRoute, _proposedTenantExistingTenureTenancyRef, It.IsAny<Guid>()), Times.Once);
            _logger.VerifyExact(LogLevel.Debug, $"Calling Income API with tenancy ref: {_proposedTenantExistingTenureTenancyRef}", Times.AtLeastOnce());
        }
    }
}
