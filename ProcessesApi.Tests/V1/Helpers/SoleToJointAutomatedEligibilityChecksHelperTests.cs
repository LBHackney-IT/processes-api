using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Hackney.Shared.Person;
using Hackney.Shared.Person.Domain;
using Hackney.Shared.Tenure.Domain;
using Moq;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Gateways.Exceptions;
using ProcessesApi.V1.Helpers;
using Xunit;

namespace ProcessesApi.Tests.V1.Helpers
{
    [Collection("LogCall collection")]
    public class SoleToJointAutomatedEligibilityChecksHelperTests
    {
        private readonly Fixture _fixture = new Fixture();
        private Mock<IIncomeApiGateway> _mockIncomeApiGateway;
        private Mock<IPersonDbGateway> _mockPersonGateway;
        private Mock<ITenureDbGateway> _mockTenureGateway;
        private SoleToJointAutomatedEligibilityChecksHelper _classUnderTest;

        public SoleToJointAutomatedEligibilityChecksHelperTests()
        {
            _mockIncomeApiGateway = new Mock<IIncomeApiGateway>();
            _mockPersonGateway = new Mock<IPersonDbGateway>();
            _mockTenureGateway = new Mock<ITenureDbGateway>();

            _classUnderTest = new SoleToJointAutomatedEligibilityChecksHelper(_mockIncomeApiGateway.Object, _mockPersonGateway.Object, _mockTenureGateway.Object);
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

        private async Task<bool> SetupAndCheckAutomatedEligibility(TenureInformation tenure, Person proposedTenant, Guid tenantId)
        {
            _mockTenureGateway.Setup(x => x.GetTenureById(tenure.Id)).ReturnsAsync(tenure);
            _mockPersonGateway.Setup(x => x.GetPersonById(proposedTenant.Id)).ReturnsAsync(proposedTenant);
            // Act
            return await _classUnderTest.CheckAutomatedEligibility(tenure.Id, proposedTenant.Id, tenantId).ConfigureAwait(false);
        }


        [Fact]
        public async Task CheckAutomatedEligibilityReturnsTrueIfAllConditionsAreMet()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();

            var tenures = proposedTenant.Tenures.Append(_fixture.Build<TenureDetails>()
                                                                .With(x => x.Type, TenureTypes.NonSecure.Code)
                                                                .With(x => x.EndDate, DateTime.UtcNow.AddDays(100).ToString())
                                                                .Create());
            proposedTenant.Tenures = tenures;
            // Act
            var response = await SetupAndCheckAutomatedEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeTrue();
            _classUnderTest.EligibilityResults.Should().HaveCount(8);
        }

        [Fact]
        public void CheckAutomatedEligibilityThrowsErrorIfTheTargetTenureIsNotFound()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();
            _mockPersonGateway.Setup(x => x.GetPersonById(proposedTenant.Id)).ReturnsAsync(proposedTenant);
            // Act
            Func<Task<bool>> func = async () => await _classUnderTest.CheckAutomatedEligibility(tenure.Id, proposedTenant.Id, tenantId)
                                                                     .ConfigureAwait(false);
            // Assert
            func.Should().Throw<TenureNotFoundException>().WithMessage($"Tenure with id {tenure.Id} not found.");
        }

        [Fact]
        public void CheckAutomatedEligibilityThrowsErrorIfTheProposedTenantIsNotFound()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();
            _mockTenureGateway.Setup(x => x.GetTenureById(tenure.Id)).ReturnsAsync(tenure);
            // Act
            Func<Task<bool>> func = async () => await _classUnderTest.CheckAutomatedEligibility(tenure.Id, proposedTenant.Id, tenantId)
                                                                     .ConfigureAwait(false);
            // Assert
            func.Should().Throw<PersonNotFoundException>().WithMessage($"Person with id {proposedTenant.Id} not found.");
        }

        [Fact]
        public async Task CheckAutomatedEligibilityReturnsFalseIfTenantIsNotANamedTenureHolderOfTheSelectedTenure()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();

            var householdMembers = tenure.HouseholdMembers;
            householdMembers.FirstOrDefault(x => x.Id == tenantId).PersonTenureType = PersonTenureType.Occupant;
            tenure.HouseholdMembers = householdMembers;
            // Act
            var response = await SetupAndCheckAutomatedEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();

            _classUnderTest.EligibilityResults["BR2"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            _classUnderTest.EligibilityResults.Should().HaveCount(8);
        }

        [Fact]
        public async Task CheckAutomatedEligibilityFailsIfTheTenantIsAlreadyPartOfAJointTenancy()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();
            var householdMembers = tenure.HouseholdMembers.Append(_fixture.Build<HouseholdMembers>()
                                                                          .With(x => x.IsResponsible, true)
                                                                          .Create());
            tenure.HouseholdMembers = householdMembers;

            // Act
            var response = await SetupAndCheckAutomatedEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();

            _classUnderTest.EligibilityResults["BR3"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            _classUnderTest.EligibilityResults.Should().HaveCount(8);
        }

        [Fact]
        public async Task CheckAutomatedEligibilityReturnsFalseIfTheSelectedTenureIsNotSecure()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();
            tenure.TenureType = TenureTypes.NonSecure;
            // Act
            var response = await SetupAndCheckAutomatedEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();

            _classUnderTest.EligibilityResults["BR4"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            _classUnderTest.EligibilityResults.Should().HaveCount(8);
        }

        [Fact]
        public async Task CheckAutomatedEligibilityReturnsFalseIfTheSelectedTenureIsNotCurrentlyActive()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();
            tenure.EndOfTenureDate = DateTime.UtcNow.AddDays(-10);
            // Act
            var response = await SetupAndCheckAutomatedEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();

            _classUnderTest.EligibilityResults["BR6"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            _classUnderTest.EligibilityResults.Should().HaveCount(8);
        }

        [Fact(Skip = "Check has been temporarily moved to ManualEligibility Check")]
        public async Task CheckAutomaticEligibilityFailsIfTenantHasLivePaymentAgreements()
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
            _mockIncomeApiGateway.Setup(x => x.GetPaymentAgreementsByTenancyReference(tenancyRef, It.IsAny<Guid>())).ReturnsAsync(paymentAgreements);
            // Act
            var response = await SetupAndCheckAutomatedEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();

            _classUnderTest.EligibilityResults["BR7"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            _classUnderTest.EligibilityResults.Should().HaveCount(8);
        }

        [Fact(Skip = "Check has been temporarily moved to ManualEligibility Check")]
        public async Task CheckAutomaticEligibilityFailsIfTenantHasAnActiveNoticeOfSeekingPossession()
        {
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();

            var tenancyWithNosp = _fixture.Build<Tenancy>()
                                    .With(x => x.TenancyRef, tenancyRef)
                                    .With(x => x.NOSP, _fixture.Build<NoticeOfSeekingPossession>()
                                                                .With(x => x.Active, true)
                                                                .Create()
                                    )
                                    .Create();

            _mockIncomeApiGateway.Setup(x => x.GetTenancyByReference(tenancyRef, It.IsAny<Guid>())).ReturnsAsync(tenancyWithNosp);

            // Act
            var response = await SetupAndCheckAutomatedEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();

            _classUnderTest.EligibilityResults["BR8"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            _classUnderTest.EligibilityResults.Should().HaveCount(8);
        }

        [Fact]
        public async Task CheckAutomatedEligibilityReturnsFalseIfTheProposedTenantIsAMinor()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();
            proposedTenant.DateOfBirth = DateTime.UtcNow;
            // Act
            var response = await SetupAndCheckAutomatedEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();

            _classUnderTest.EligibilityResults["BR19"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            _classUnderTest.EligibilityResults.Should().HaveCount(8);
        }

        [Fact]
        public async Task CheckAutomatedEligibilityReturnsFalseIfTheProposedTenantIsAHouseholdMemberOfAnActiveTenureThatIsNotNonSecure()
        {
            // Arrange
            (var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateEligibleTenureAndProposedTenant();

            var tenures = proposedTenant.Tenures.Append(_fixture.Build<TenureDetails>()
                                                                .With(x => x.Type, TenureTypes.Freehold.Code)
                                                                .With(x => x.EndDate, DateTime.UtcNow.AddDays(100).ToString())
                                                                .Create());
            proposedTenant.Tenures = tenures;
            // Act
            var response = await SetupAndCheckAutomatedEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            response.Should().BeFalse();

            _classUnderTest.EligibilityResults["BR9"].Should().BeFalse();
            _classUnderTest.EligibilityResults.Count(x => x.Value == false).Should().Be(1);
            _classUnderTest.EligibilityResults.Should().HaveCount(8);
        }
    }
}
