using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Shared.Person;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Infrastructure;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Infrastructure;
using System;
using System.Net.Http;
using System.Threading.Tasks;
using ProcessesApi.Tests.V1.E2ETests.Steps;
using Hackney.Core.Testing.Sns;
using System.Text.Json;
using System.Collections.Generic;
using Hackney.Shared.Processes.Constants;
using Hackney.Shared.Processes.Constants.SoleToJoint;
using SoleToJointKeys = Hackney.Shared.Processes.Constants.SoleToJoint.SoleToJointKeys;

namespace ProcessesApi.Tests.V1.E2E.Steps
{
    public class SoleToJointSteps : UpdateProcessBaseSteps
    {
        private readonly IDynamoDbFixture _dbFixture;

        public SoleToJointSteps(HttpClient httpClient, IDynamoDbFixture dbFixture) : base(httpClient, dbFixture)
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
        public async Task ThenTheProcessStateIsUpdatedToUpdateTenure(UpdateProcessQuery request, UpdateProcessRequestObject requestObject, string initialState, Guid incomingTenantId)
        {
            await CheckProcessState(request.Id, SoleToJointStates.TenureUpdated, initialState).ConfigureAwait(false);

            var process = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(request.Id).ConfigureAwait(false);
            // RelatedEntities
            var newTenureDetails = process.RelatedEntities.Find(x => x.TargetType == TargetType.tenure
                                                              && x.SubType == SubType.newTenure);
            newTenureDetails.Should().NotBeNull();
            var tenureDate = DateTime.Parse(requestObject.FormData[SoleToJointKeys.TenureStartDate].ToString());
            // oldTenure
            var oldTenure = await _dbFixture.DynamoDbContext.LoadAsync<TenureInformationDb>(process.TargetId).ConfigureAwait(false);
            oldTenure.EndOfTenureDate.Should().Be(tenureDate);
            // newTenure
            var newTenure = await _dbFixture.DynamoDbContext.LoadAsync<TenureInformationDb>(newTenureDetails.Id).ConfigureAwait(false);
            newTenure.StartOfTenureDate.Should().Be(tenureDate);
            newTenure.Should().BeEquivalentTo(oldTenure, c => c.Excluding(x => x.Id)
                                                               .Excluding(x => x.HouseholdMembers)
                                                               .Excluding(x => x.StartOfTenureDate)
                                                               .Excluding(x => x.EndOfTenureDate)
                                                               .Excluding(x => x.VersionNumber));
            var householdMember = newTenure.HouseholdMembers.Find(x => x.Id == incomingTenantId);
            householdMember.PersonTenureType.Should().Be(PersonTenureType.Tenant);
        }

        public async Task ThenTheProcessUpdatedEventIsRaisedWithNewTenureIdAndStartDate(ISnsFixture snsFixture, Guid processId, string oldState, string newState)
        {
            Action<string> verifyData = (dataAsString) =>
            {
                var dataDic = JsonSerializer.Deserialize<Dictionary<string, object>>(dataAsString, _jsonOptions);
                var stateData = JsonSerializer.Deserialize<Dictionary<string, object>>(dataDic["stateData"].ToString(), _jsonOptions);
                stateData.Should().ContainKey(SoleToJointKeys.NewTenureId);
                stateData.Should().ContainKey(SoleToJointKeys.TenureStartDate);
            };

            await VerifyProcessUpdatedEventIsRaised(snsFixture, processId, oldState, newState, verifyData).ConfigureAwait(false);
        }
    }
}
