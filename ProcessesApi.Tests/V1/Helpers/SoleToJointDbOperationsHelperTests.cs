using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Hackney.Shared.Person;
using Hackney.Shared.Person.Boundary.Request;
using Hackney.Shared.Person.Boundary.Response;
using Hackney.Shared.Person.Domain;
using Hackney.Shared.Tenure.Boundary.Requests;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Factories;
using Hackney.Shared.Tenure.Infrastructure;
using Moq;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Gateways.Exceptions;
using ProcessesApi.V1.Helpers;
using ProcessesApi.V1.Services.Exceptions;
using Xunit;

namespace ProcessesApi.Tests.V1.Helpers
{
    [Collection("LogCall collection")]
    public class SoleToJointDbOperationsHelperTests
    {
        private readonly Fixture _fixture = new Fixture();
        private Mock<IIncomeApiGateway> _mockIncomeApi;
        private Mock<IPersonDbGateway> _mockPersonDb;
        private Mock<ITenureDbGateway> _mockTenureDb;
        private Mock<IPersonApiGateway> _mockPersonApi;
        private Mock<ITenureApiGateway> _mockTenureApi;
        private SoleToJointDbOperationsHelper _classUnderTest;

        public SoleToJointDbOperationsHelperTests()
        {
            _mockIncomeApi = new Mock<IIncomeApiGateway>();
            _mockPersonDb = new Mock<IPersonDbGateway>();
            _mockTenureDb = new Mock<ITenureDbGateway>();
            _mockPersonApi = new Mock<IPersonApiGateway>();
            _mockTenureApi = new Mock<ITenureApiGateway>();

            _classUnderTest = new SoleToJointDbOperationsHelper(_mockIncomeApi.Object,
                                                                _mockPersonDb.Object,
                                                                _mockTenureDb.Object,
                                                                _mockPersonApi.Object,
                                                                _mockTenureApi.Object);
        }

        private (Process, Person, TenureInformation, Guid, string) CreateProcessAndRelatedEntities()
        {
            var proposedTenantId = Guid.NewGuid();
            var tenancyRef = _fixture.Create<string>();

            var tenant = _fixture.Build<HouseholdMembers>()
                                 .With(x => x.PersonTenureType, PersonTenureType.Tenant)
                                 .With(x => x.IsResponsible, true)
                                 .Create();
            var tenantRelatedEntity = new RelatedEntity
            {
                Id = tenant.Id,
                TargetType = TargetType.person,
                SubType = SubType.tenant
            };

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

            var proposedTenantRelatedEntity = new RelatedEntity
            {
                Id = proposedTenant.Id,
                TargetType = TargetType.person,
                SubType = SubType.householdMember
            };

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
            var process = _fixture.Build<Process>().With(x => x.TargetId, tenure.Id).Create();
            process.RelatedEntities = new List<RelatedEntity> { tenantRelatedEntity, proposedTenantRelatedEntity };

            return (process, proposedTenant, tenure, tenant.Id, tenancyRef);
        }

        private async Task<bool> SetupAndCheckAutomatedEligibility(TenureInformation tenure, Person proposedTenant, Guid tenantId)
        {
            _mockTenureDb.Setup(x => x.GetTenureById(tenure.Id)).ReturnsAsync(tenure);
            _mockPersonDb.Setup(x => x.GetPersonById(proposedTenant.Id)).ReturnsAsync(proposedTenant);
            // Act
            return await _classUnderTest.CheckAutomatedEligibility(tenure.Id, proposedTenant.Id, tenantId).ConfigureAwait(false);
        }

        #region Automated Eligibility checks

        [Fact]
        public async Task CheckAutomatedEligibilityReturnsTrueIfAllConditionsAreMet()
        {
            // Arrange
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();

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
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();
            _mockPersonDb.Setup(x => x.GetPersonById(proposedTenant.Id)).ReturnsAsync(proposedTenant);
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
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();
            _mockTenureDb.Setup(x => x.GetTenureById(tenure.Id)).ReturnsAsync(tenure);
            // Act
            Func<Task<bool>> func = async () => await _classUnderTest.CheckAutomatedEligibility(tenure.Id, proposedTenant.Id, tenantId)
                                                                     .ConfigureAwait(false);
            // Assert
            func.Should().Throw<PersonNotFoundException>().WithMessage($"Person with id {proposedTenant.Id} not found.");
        }

        [Fact]
        public void CheckAutomatedEligibilityThrowsErrorIfTheCurrentTenantIsNotListedAsAHouseholdMember()
        {
            // Arrange
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();
            tenure.HouseholdMembers = tenure.HouseholdMembers.Where(x => x.Id != tenantId);
            // Act
            Func<Task<bool>> func = async () => await SetupAndCheckAutomatedEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            func.Should().Throw<FormDataInvalidException>()
                .WithMessage($"The request's FormData is invalid: The tenant with ID {tenantId} is not listed as a household member of the tenure with ID {tenure.Id}");
        }

        [Fact]
        public async Task CheckAutomatedEligibilityReturnsFalseIfTenantIsNotANamedTenureHolderOfTheSelectedTenure()
        {
            // Arrange
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();

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
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();
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
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();
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
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();
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
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();
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
            _mockIncomeApi.Setup(x => x.GetPaymentAgreementsByTenancyReference(tenancyRef, It.IsAny<Guid>())).ReturnsAsync(paymentAgreements);
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
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();

            var tenancyWithNosp = _fixture.Build<Tenancy>()
                                    .With(x => x.TenancyRef, tenancyRef)
                                    .With(x => x.NOSP, _fixture.Build<NoticeOfSeekingPossession>()
                                                                .With(x => x.Active, true)
                                                                .Create()
                                    )
                                    .Create();

            _mockIncomeApi.Setup(x => x.GetTenancyByReference(tenancyRef, It.IsAny<Guid>())).ReturnsAsync(tenancyWithNosp);

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
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();
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
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();

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

        #endregion

        #region Update Tenure

        private bool VerifyEndExistingTenure(EditTenureDetailsRequestObject requestObject)
        {
            requestObject.EndOfTenureDate.Should().BeCloseTo(DateTime.UtcNow, 2000);
            return true;
        }

        private bool VerifyNewTenure(CreateTenureRequestObject requestObject, TenureInformationDb oldTenure, Guid incomingTenantId)
        {
            var newTenure = requestObject.ToDatabase();
            newTenure.Should().BeEquivalentTo(oldTenure, c => c.Excluding(x => x.Id)
                                                               .Excluding(x => x.HouseholdMembers)
                                                               .Excluding(x => x.StartOfTenureDate));

            requestObject.StartOfTenureDate.Should().BeCloseTo(DateTime.UtcNow, 2000);

            var householdMemberDetails = newTenure.HouseholdMembers.Find(x => x.Id == incomingTenantId);
            householdMemberDetails.IsResponsible.Should().BeTrue();
            householdMemberDetails.PersonTenureType.Should().Be(PersonTenureType.Tenant);

            return true;
        }

        [Fact]
        public async Task EndsExistingTenureAndCreatesNewTenureWithAddedTenant()
        {
            (var process, var proposedTenant, var oldTenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();

            _mockTenureDb.Setup(x => x.GetTenureById(oldTenure.Id)).ReturnsAsync(oldTenure);
            _mockPersonDb.Setup(x => x.GetPersonById(proposedTenant.Id)).ReturnsAsync(proposedTenant);

            var newTenure = _fixture.Create<TenureResponseObject>();
            _mockTenureApi.Setup(x => x.CreateNewTenure(It.IsAny<CreateTenureRequestObject>())).ReturnsAsync(newTenure);

            // Act
            await _classUnderTest.UpdateTenures(process).ConfigureAwait(false);

            // Assert

            _mockTenureDb.Verify(g => g.GetTenureById(oldTenure.Id), Times.Once);

            _mockTenureApi.Verify(g => g.EditTenureDetailsById(oldTenure.Id, It.Is<EditTenureDetailsRequestObject>(x => VerifyEndExistingTenure(x)), oldTenure.VersionNumber), Times.Once);
            _mockTenureApi.Verify(g => g.CreateNewTenure(It.Is<CreateTenureRequestObject>(x => VerifyNewTenure(x, oldTenure.ToDatabase(), proposedTenant.Id))), Times.Once);
        }

        #endregion
    }
}
