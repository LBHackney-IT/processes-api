using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using FluentAssertions;
using Hackney.Core.JWT;
using Hackney.Core.Sns;
using Hackney.Shared.Person;
using Hackney.Shared.Person.Domain;
using Hackney.Shared.Tenure.Boundary.Requests;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Factories;
using Hackney.Shared.Tenure.Infrastructure;
using Moq;
using Hackney.Shared.Processes.Domain;
using Hackney.Shared.Processes.Factories;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Gateways.Exceptions;
using ProcessesApi.V1.Helpers;
using Hackney.Shared.Processes.Infrastructure;
using ProcessesApi.V1.Services.Exceptions;
using Xunit;
using ProcessesApi.V1.Domain.Finance;
using Hackney.Shared.Person.Infrastructure;
using Hackney.Shared.Person.Boundary.Request;
using Hackney.Shared.Processes.Domain.Constants.ChangeOfName;
using Hackney.Shared.Processes.Domain.Constants.SoleToJoint;
using ChangeOfNameKeys = Hackney.Shared.Processes.Domain.Constants.ChangeOfName.ChangeOfNameKeys;
using ProcessesApi.V1.Factories;

namespace ProcessesApi.Tests.V1.Helpers
{
    [Collection("LogCall collection")]
    public class DbOperationsHelperTests
    {
        private readonly Fixture _fixture = new Fixture();
        private Mock<IIncomeApiGateway> _mockIncomeApi;
        private Mock<IPersonDbGateway> _mockPersonDb;
        private Mock<ITenureDbGateway> _mockTenureDb;
        private Mock<ISnsGateway> _mockSnsGateway;
        private DbOperationsHelper _classUnderTest;

        public DbOperationsHelperTests()
        {
            _mockIncomeApi = new Mock<IIncomeApiGateway>();
            _mockPersonDb = new Mock<IPersonDbGateway>();
            _mockTenureDb = new Mock<ITenureDbGateway>();
            _mockSnsGateway = new Mock<ISnsGateway>();

            _classUnderTest = new DbOperationsHelper(_mockIncomeApi.Object,
                                                                _mockPersonDb.Object,
                                                                _mockTenureDb.Object,
                                                                new TenureSnsFactory(),
                                                                new PersonSnsFactory(),
                                                                _mockSnsGateway.Object);
        }

        [Fact]
        public async Task AddsIncomingTenantToRelatedEntities()
        {
            // arrange
            var process = _fixture.Create<Process>();
            var incomingTenant = _fixture.Create<Person>();
            _mockPersonDb.Setup(x => x.GetPersonById(incomingTenant.Id)).ReturnsAsync(incomingTenant);
            var formData = new Dictionary<string, object> { { SoleToJointKeys.IncomingTenantId, incomingTenant.Id } };

            // act
            await _classUnderTest.AddIncomingTenantToRelatedEntities(formData, process).ConfigureAwait(false);

            // assert
            var relatedEntity = process.RelatedEntities.Find(x => x.Id == incomingTenant.Id);
            relatedEntity.Should().NotBeNull();
            relatedEntity.TargetType.Should().Be(TargetType.person);
            relatedEntity.SubType.Should().Be(SubType.householdMember);
            relatedEntity.Description.Should().Be($"{incomingTenant.FirstName} {incomingTenant.Surname}");
        }

        #region Helpers
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

        private (Process, Person) CreateProcessAndPerson()
        {
            var person = _fixture.Create<Person>();
            var process = _fixture.Build<Process>().With(x => x.TargetId, person.Id).Create();
            process.PreviousStates.Add(_fixture.Build<ProcessState>()
                                               .With(x => x.State, ChangeOfNameStates.NameSubmitted)
                                               .Create()
                                      );
            process.PreviousStates.Find(x => x.State == ChangeOfNameStates.NameSubmitted).ProcessData.FormData.Add(ChangeOfNameKeys.FirstName, "NewFirstName");
            process.PreviousStates.Find(x => x.State == ChangeOfNameStates.NameSubmitted).ProcessData.FormData.Add(ChangeOfNameKeys.Surname, "NewSurname");
            process.PreviousStates.Find(x => x.State == ChangeOfNameStates.NameSubmitted).ProcessData.FormData.Add(ChangeOfNameKeys.Title, Title.Miss);
            return (process, person);
        }

        private async Task<bool> SetupAndCheckAutomatedEligibility(TenureInformation tenure, Person proposedTenant, Guid tenantId)
        {
            _mockTenureDb.Setup(x => x.GetTenureById(tenure.Id)).ReturnsAsync(tenure);
            _mockPersonDb.Setup(x => x.GetPersonById(proposedTenant.Id)).ReturnsAsync(proposedTenant);
            // Act
            return await _classUnderTest.CheckAutomatedEligibility(tenure.Id, proposedTenant.Id, tenantId).ConfigureAwait(false);
        }

        #endregion

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
        public async Task CheckAutomatedEligibilityThrowsErrorIfTheTargetTenureIsNotFound()
        {
            // Arrange
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();
            _mockPersonDb.Setup(x => x.GetPersonById(proposedTenant.Id)).ReturnsAsync(proposedTenant);
            // Act
            Func<Task<bool>> func = async () => await _classUnderTest.CheckAutomatedEligibility(tenure.Id, proposedTenant.Id, tenantId)
                                                                     .ConfigureAwait(false);
            // Assert
            (await func.Should().ThrowAsync<TenureNotFoundException>()).WithMessage($"Tenure with id {tenure.Id} not found.");
        }

        [Fact]
        public async Task CheckAutomatedEligibilityThrowsErrorIfTheProposedTenantIsNotFound()
        {
            // Arrange
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();
            _mockTenureDb.Setup(x => x.GetTenureById(tenure.Id)).ReturnsAsync(tenure);
            // Act
            Func<Task<bool>> func = async () => await _classUnderTest.CheckAutomatedEligibility(tenure.Id, proposedTenant.Id, tenantId)
                                                                     .ConfigureAwait(false);
            // Assert
            (await func.Should().ThrowAsync<PersonNotFoundException>()).WithMessage($"Person with id {proposedTenant.Id} not found.");
        }

        [Fact]
        public async Task CheckAutomatedEligibilityThrowsErrorIfTheCurrentTenantIsNotListedAsAHouseholdMember()
        {
            // Arrange
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();
            tenure.HouseholdMembers = tenure.HouseholdMembers.Where(x => x.Id != tenantId);
            // Act
            Func<Task<bool>> func = async () => await SetupAndCheckAutomatedEligibility(tenure, proposedTenant, tenantId).ConfigureAwait(false);
            // Assert
            (await func.Should().ThrowAsync<FormDataInvalidException>())
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

        private bool VerifyEndExistingTenure(EditTenureDetailsRequestObject requestObject, TenureInformation oldTenure, DateTime tenureStartDate)
        {
            requestObject.EndOfTenureDate.Should().Be(tenureStartDate);
            return true;
        }

        private bool VerifyNewTenure(CreateTenureRequestObject requestObject, TenureInformationDb oldTenure, Guid incomingTenantId, DateTime tenureStartDate)
        {
            var newTenure = requestObject.ToDatabase();
            newTenure.Should().BeEquivalentTo(oldTenure, c => c.Excluding(x => x.Id)
                                                               .Excluding(x => x.HouseholdMembers)
                                                               .Excluding(x => x.PaymentReference)
                                                               .Excluding(x => x.StartOfTenureDate));
            requestObject.StartOfTenureDate.Should().Be(tenureStartDate);
            newTenure.PaymentReference.Should().BeNull();
            newTenure.HouseholdMembers.Should().HaveSameCount(oldTenure.HouseholdMembers);

            var householdMember = newTenure.HouseholdMembers.Find(x => x.Id == incomingTenantId);
            householdMember.PersonTenureType.Should().Be(PersonTenureType.Tenant);
            householdMember.IsResponsible.Should().BeTrue();
            return true;
        }

        private bool VerifyUpdateTenureForPerson(UpdateTenureForPersonRequestObject requestObject)
        {
            requestObject.IsResponsible.Should().BeTrue();
            requestObject.Type.Should().Be(HouseholdMembersType.Person);

            return true;
        }

        [Fact]
        public async Task EndsExistingTenureAndCreatesNewTenureWithAddedTenant()
        {
            (var process, var proposedTenant, var tenure, var tenantId, var tenancyRef) = CreateProcessAndRelatedEntities();
            var tenureStartDate = DateTime.Parse(_fixture.Create<DateTime>().ToLongDateString());

            _mockTenureDb.Setup(x => x.GetTenureById(tenure.Id)).ReturnsAsync(tenure);

            var updateResult = _fixture.Create<UpdateEntityResult<TenureInformationDb>>();
            _mockTenureDb.Setup(x => x.UpdateTenureById(tenure.Id, It.IsAny<EditTenureDetailsRequestObject>())).ReturnsAsync(updateResult);

            _mockTenureDb.Setup(x => x.PostNewTenureAsync(It.IsAny<CreateTenureRequestObject>())).ReturnsAsync(tenure.ToDatabase());

            // Act
            await _classUnderTest.UpdateTenures(process,
                                                new Token(),
                                                new Dictionary<string, object> { { SoleToJointKeys.TenureStartDate, tenureStartDate } })
                                 .ConfigureAwait(false);

            // Assert
            _mockTenureDb.Verify(g => g.GetTenureById(tenure.Id), Times.Once);
            _mockTenureDb.Verify(g => g.UpdateTenureById(tenure.Id, It.Is<EditTenureDetailsRequestObject>(x => VerifyEndExistingTenure(x, tenure, tenureStartDate))), Times.Once);
            _mockTenureDb.Verify(g => g.PostNewTenureAsync(It.Is<CreateTenureRequestObject>(x => VerifyNewTenure(x, tenure.ToDatabase(), proposedTenant.Id, tenureStartDate))), Times.Once);

            var numberOfEvents = tenure.HouseholdMembers.Count() + 2; // 1 event per hm, plus on update old tenure & on create new tenure
            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Exactly(numberOfEvents));
        }

        #endregion

        #region UpdateName

        private bool VerifyUpdateName(UpdatePersonRequestObject requestObject, Process process)
        {
            var updatedNameData = process.PreviousStates.Find(x => x.State == ChangeOfNameStates.NameSubmitted).ProcessData.FormData;
            requestObject.FirstName.Should().Be(updatedNameData.GetValueOrDefault(ChangeOfNameKeys.FirstName).ToString());
            requestObject.Surname.Should().Be(updatedNameData.GetValueOrDefault(ChangeOfNameKeys.Surname).ToString());
            requestObject.Title.Should().Be((Title) Enum.Parse(typeof(Title), updatedNameData.GetValueOrDefault(ChangeOfNameKeys.Title).ToString()));
            return true;
        }

        [Fact]
        public async Task CallsDbToUpdatePersonWithNewName()
        {
            (var process, var person) = CreateProcessAndPerson();

            _mockPersonDb.Setup(x => x.GetPersonById(person.Id)).ReturnsAsync(person);

            var updateResult = _fixture.Create<UpdateEntityResult<PersonDbEntity>>();
            _mockPersonDb.Setup(x => x.UpdatePersonByIdAsync(person.Id, It.IsAny<UpdatePersonRequestObject>())).ReturnsAsync(updateResult);

            await _classUnderTest.UpdatePerson(process, new Token()).ConfigureAwait(false);

            _mockPersonDb.Verify(g => g.GetPersonById(person.Id), Times.Once);
            _mockPersonDb.Verify(g => g.UpdatePersonByIdAsync(person.Id, It.Is<UpdatePersonRequestObject>(x => VerifyUpdateName(x, process))), Times.Once);
            _mockSnsGateway.Verify(g => g.Publish(It.IsAny<EntityEventSns>(), It.IsAny<string>(), It.IsAny<string>()), Times.Once);

        }


        #endregion
    }
}
