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
using Microsoft.Extensions.Logging;
using Moq;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Gateways.Exceptions;
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
        private readonly IDynamoDbFixture _dbFixture;
        private readonly Fixture _fixture = new Fixture();
        private SoleToJointGateway _classUnderTest;
        private readonly List<Action> _cleanup = new List<Action>();
        private readonly Mock<ILogger<SoleToJointGateway>> _logger;
        private readonly Mock<IApiGateway> _mockApiGateway;

        private const string IncomeApiRoute = "https://some-domain.com/api";
        private const string IncomeApiToken = "dksfghjskueygfakseygfaskjgfsdjkgfdkjsgfdkjgf";
        private const string ApiName = "Income";
        private const string IncomeApiUrlKey = "IncomeApiUrl";
        private const string IncomeApiTokenKey = "IncomeApiToken";
        private static string paymentAgreementRoute => $"{IncomeApiRoute}/agreements";
        private static string tenanciesRoute => $"{IncomeApiRoute}/tenancies";

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

        private (Person, TenureInformation, Guid, string) CreateEligibleTenureAndProposedTenant()
        {
            var proposedTenantId = Guid.NewGuid();
            var tenancyRef = _fixture.Create<string>();

            var tenant = _fixture.Build<HouseholdMembers>()
                                 .With(x => x.PersonTenureType, PersonTenureType.Tenant)
                                 .With(x => x.IsResponsible, true)
                                 .Create();

            var proposedTenant = _fixture.Build<Person>()
                                         .With(x => x.VersionNumber, (int?) null)
                                         .With(x => x.Id, proposedTenantId)
                                         .With(x => x.Tenures, new List<TenureDetails>())
                                         .With(x => x.DateOfBirth, DateTime.UtcNow.AddYears(-18))
                                         .Create();
            var proposedTenantHouseholdMember = _fixture.Build<HouseholdMembers>()
                                            .With(x => x.Id, proposedTenantId)
                                            .With(x => x.IsResponsible, false)
                                            .Create();

            var tenure = _fixture.Build<TenureInformation>()
                        .With(x => x.HouseholdMembers, new List<HouseholdMembers> { proposedTenantHouseholdMember, tenant })
                        .With(x => x.TenureType, TenureTypes.Secure)
                        .With(x => x.EndOfTenureDate, (DateTime?) null)
                        .With(x => x.LegacyReferences, new List<LegacyReference> {
                            new LegacyReference { Name = "uh_tag_ref", Value = tenancyRef }
                        })
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();

            var personTenures = proposedTenant.Tenures.Append(_fixture.Build<TenureDetails>()
                                                                      .With(x => x.Id, tenure.Id)
                                                                      .With(x => x.EndDate, DateTime.UtcNow.AddYears(10).ToString())
                                                                      .With(x => x.Type, TenureTypes.Secure.Code)
                                                                      .Create());
            proposedTenant.Tenures = personTenures;

            return (proposedTenant, tenure, tenant.Id, tenancyRef);
        }

        private async Task<bool> SaveAndCheckEligibility(TenureInformation tenure, Person proposedTenant, Guid tenantId)
        {
            await _dbFixture.SaveEntityAsync(tenure.ToDatabase()).ConfigureAwait(false);
            await _dbFixture.SaveEntityAsync(proposedTenant.ToDatabase()).ConfigureAwait(false);
            // Act
            var response = await _classUnderTest.CheckEligibility(tenure.Id, proposedTenant.Id, tenantId).ConfigureAwait(false);
            return response;
        }

        private void AllTestsShouldHaveRun(TenureInformation tenure, Person proposedTenant, string tenancyRef)
        {
            _classUnderTest.EligibilityResults.Should().HaveCount(8);
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.Once());
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Person ID: {proposedTenant.Id}", Times.Once());
            _logger.VerifyExact(LogLevel.Debug, $"Calling Income API for payment agreement with tenancy ref: {tenancyRef}", Times.Once());
            _logger.VerifyExact(LogLevel.Debug, $"Calling Income API with tenancy ref: {tenancyRef}", Times.Once());
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
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();

            var tenures = proposedTenant.Tenures.Append(_fixture.Build<TenureDetails>()
                                                                .With(x => x.Type, TenureTypes.NonSecure.Code)
                                                                .With(x => x.EndDate, DateTime.UtcNow.AddDays(100).ToString())
                                                                .Create());
            proposedTenant.Tenures = tenures;
            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeTrue();
            AllTestsShouldHaveRun(tenure, proposedTenant, tenancyRef);
        }

        [Fact]
        public async Task CheckEligibilityThrowsErrorIfTheTargetTenureIsNotFound()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();
            await _dbFixture.SaveEntityAsync(proposedTenant.ToDatabase()).ConfigureAwait(false);
            // Act
            Func<Task<bool>> func = async () => await _classUnderTest.CheckEligibility(tenure.Id, proposedTenant.Id, tenantId).ConfigureAwait(false);
            // Assert
            func.Should().Throw<TenureNotFoundException>().WithMessage($"Tenure with id {tenure.Id} not found.");
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.Once());
        }

        [Fact]
        public async Task CheckEligibilityThrowsErrorIfTheProposedTenantIsNotFound()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();
            await _dbFixture.SaveEntityAsync(tenure.ToDatabase()).ConfigureAwait(false);
            // Act
            Func<Task<bool>> func = async () => await _classUnderTest.CheckEligibility(tenure.Id, proposedTenant.Id, tenantId).ConfigureAwait(false);
            // Assert
            func.Should().Throw<PersonNotFoundException>().WithMessage($"Person with id {proposedTenant.Id} not found.");
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Tenure ID: {tenure.Id}", Times.Once());
            _logger.VerifyExact(LogLevel.Debug, $"Calling IDynamoDBContext.LoadAsync for Person ID: {proposedTenant.Id}", Times.Once());
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTenantIsNotANamedTenureHolderOfTheSelectedTenure()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();

            var householdMembers = tenure.HouseholdMembers;
            householdMembers.FirstOrDefault(x => x.Id == tenantId).PersonTenureType = PersonTenureType.Occupant;
            tenure.HouseholdMembers = householdMembers;
            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _classUnderTest.EligibilityResults["BR2"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            AllTestsShouldHaveRun(tenure, proposedTenant, tenancyRef);
        }

        [Fact]
        public async Task CheckEligibiltyFailsIfTheTenantIsAlreadyPartOfAJointTenancy()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();
            var householdMembers = tenure.HouseholdMembers.Append(_fixture.Build<HouseholdMembers>()
                                                                          .With(x => x.IsResponsible, true)
                                                                          .Create());
            tenure.HouseholdMembers = householdMembers;

            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _classUnderTest.EligibilityResults["BR3"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            AllTestsShouldHaveRun(tenure, proposedTenant, tenancyRef);
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheSelectedTenureIsNotSecure()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();
            tenure.TenureType = TenureTypes.NonSecure;
            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _classUnderTest.EligibilityResults["BR4"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            AllTestsShouldHaveRun(tenure, proposedTenant, tenancyRef);
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheSelectedTenureIsNotCurrentlyActive()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();
            tenure.EndOfTenureDate = DateTime.UtcNow.AddDays(-10);
            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _classUnderTest.EligibilityResults["BR6"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            AllTestsShouldHaveRun(tenure, proposedTenant, tenancyRef);
        }

        [Fact]
        public async Task CheckEligibilityFailsIfTenantHasLivePaymentAgreements()
        {
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();
            var paymentAgreements = new PaymentAgreements
            {
                Agreements = new List<PaymentAgreement>
                {
                    _fixture.Build<PaymentAgreement>()
                        .With(x => x.TenancyRef, tenancyRef)
                        .With(x => x.Amount, 50)
                        .Create()
                }
            };
            _mockApiGateway.Setup(x => x.GetByIdAsync<PaymentAgreements>($"{paymentAgreementRoute}/{tenancyRef}", tenancyRef, It.IsAny<Guid>())).ReturnsAsync(paymentAgreements);
            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _classUnderTest.EligibilityResults["BR7"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            AllTestsShouldHaveRun(tenure, proposedTenant, tenancyRef);
            _mockApiGateway.Verify(x => x.GetByIdAsync<PaymentAgreements>($"{paymentAgreementRoute}/{tenancyRef}", tenancyRef, It.IsAny<Guid>()), Times.Once);
        }

        [Fact]
        public async Task CheckEligibilityFailsIfTenantHasAnActiveNoticeOfSeekingPossession()
        {
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();

            var tenancyWithNosp = _fixture.Build<Tenancy>()
                                    .With(x => x.TenancyRef, tenancyRef)
                                    .With(x => x.nosp, _fixture.Build<NoticeOfSeekingPossession>()
                                                                .With(x => x.active, true)
                                                                .Create()
                                    )
                                    .Create();

            _mockApiGateway.Setup(x => x.GetByIdAsync<Tenancy>($"{tenanciesRoute}/{tenancyRef}", tenancyRef, It.IsAny<Guid>())).ReturnsAsync(tenancyWithNosp);

            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _classUnderTest.EligibilityResults["BR8"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            AllTestsShouldHaveRun(tenure, proposedTenant, tenancyRef);
            _mockApiGateway.Verify(x => x.GetByIdAsync<Tenancy>($"{tenanciesRoute}/{tenancyRef}", tenancyRef, It.IsAny<Guid>()), Times.Once);
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheProposedTenantIsAMinor()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();
            proposedTenant.DateOfBirth = DateTime.UtcNow;
            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _classUnderTest.EligibilityResults["BR19"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            AllTestsShouldHaveRun(tenure, proposedTenant, tenancyRef);
        }

        [Fact]
        public async Task CheckEligibiltyReturnsFalseIfTheProposedTenantIsAHouseholdMemberOfAnActiveTenureThatIsNotNonSecure()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();

            var tenures = proposedTenant.Tenures.Append(_fixture.Build<TenureDetails>()
                                                                .With(x => x.Type, TenureTypes.Freehold.Code)
                                                                .With(x => x.EndDate, DateTime.UtcNow.AddDays(100).ToString())
                                                                .Create());
            proposedTenant.Tenures = tenures;
            // Act
            var response = await SaveAndCheckEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();
            _classUnderTest.EligibilityResults["BR9"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            AllTestsShouldHaveRun(tenure, proposedTenant, tenancyRef);
        }
    }
}
