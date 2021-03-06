using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Shared.Person;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Infrastructure;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Constants.SoleToJoint;
using ProcessesApi.V1.Infrastructure;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using ProcessesApi.V1.Constants;
using ProcessesApi.Tests.V1.E2ETests.Steps;

namespace ProcessesApi.Tests.V1.E2E.Steps
{
    public class UpdateSoleToJointProcessSteps : UpdateProcessBaseSteps
    {
        private readonly IDynamoDbFixture _dbFixture;

        public UpdateSoleToJointProcessSteps(HttpClient httpClient, IDynamoDbFixture dbFixture) : base(httpClient, dbFixture)
        {
            _dbFixture = dbFixture;
        }

        public async Task ThenTheIncomingTenantIdIsAddedToRelatedEntities(UpdateProcessQuery request, UpdateProcessRequestObject requestBody, Person person)
        {
            var dbRecord = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(request.Id).ConfigureAwait(false);

            var incomingTenantId = Guid.Parse(requestBody.FormData[SoleToJointKeys.IncomingTenantId].ToString());
            var relatedEntity = dbRecord.RelatedEntities.Find(x => x.Id == incomingTenantId);
            relatedEntity.Should().NotBeNull();
            relatedEntity.TargetType.Should().Be(TargetType.person);
            relatedEntity.SubType.Should().Be(SubType.householdMember);
            relatedEntity.Description.Should().Be($"{person.FirstName} {person.Surname}");
        }

        public async Task ThenTheProcessStateIsUpdatedToAutomatedEligibilityChecksPassed(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.AutomatedChecksPassed, SoleToJointStates.SelectTenants).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToAutomatedEligibilityChecksFailed(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.AutomatedChecksFailed, SoleToJointStates.SelectTenants).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToManualChecksPassed(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.ManualChecksPassed, SoleToJointStates.AutomatedChecksPassed).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToManualChecksFailed(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.ManualChecksFailed, SoleToJointStates.AutomatedChecksPassed).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToBreachChecksPassed(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.BreachChecksPassed, SoleToJointStates.ManualChecksPassed).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToBreachChecksFailed(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.BreachChecksFailed, SoleToJointStates.ManualChecksPassed).ConfigureAwait(false);
        }


        public async Task ThenTheProcessStateIsUpdatedToDocumentsRequestedDes(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SharedStates.DocumentsRequestedDes, SoleToJointStates.BreachChecksPassed).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToDocumentsRequestedAppointment(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SharedStates.DocumentsRequestedAppointment, SoleToJointStates.BreachChecksPassed).ConfigureAwait(false);
        }
        public async Task ThenTheProcessStateIsUpdatedToUpdateTenure(UpdateProcessQuery request, string initialState, Guid incomingTenantId)
        {
            await CheckProcessState(request.Id, SoleToJointStates.TenureUpdated, initialState).ConfigureAwait(false);

            var process = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(request.Id).ConfigureAwait(false);
            // RelatedEntities
            var newTenureDetails = process.RelatedEntities.Find(x => x.TargetType == TargetType.tenure
                                                              && x.SubType == SubType.newTenure);
            newTenureDetails.Should().NotBeNull();
            // oldTenure
            var oldTenure = await _dbFixture.DynamoDbContext.LoadAsync<TenureInformationDb>(process.TargetId).ConfigureAwait(false);
            oldTenure.EndOfTenureDate.Should().BeCloseTo(DateTime.UtcNow, 3000);
            // newTenure
            var newTenure = await _dbFixture.DynamoDbContext.LoadAsync<TenureInformationDb>(newTenureDetails.Id).ConfigureAwait(false);
            newTenure.StartOfTenureDate.Should().BeCloseTo(DateTime.UtcNow, 3000);
            newTenure.Should().BeEquivalentTo(oldTenure, c => c.Excluding(x => x.Id)
                                                               .Excluding(x => x.HouseholdMembers)
                                                               .Excluding(x => x.StartOfTenureDate)
                                                               .Excluding(x => x.EndOfTenureDate)
                                                               .Excluding(x => x.VersionNumber));
            var householdMember = newTenure.HouseholdMembers.Find(x => x.Id == incomingTenantId);
            householdMember.PersonTenureType.Should().Be(PersonTenureType.Tenant);
        }
    }
}
