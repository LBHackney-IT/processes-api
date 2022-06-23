using FluentAssertions;
using Hackney.Core.Sns;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Shared.E2E;
using Hackney.Core.Testing.Sns;
using Hackney.Shared.Person;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Infrastructure;
using Newtonsoft.Json;
using ProcessesApi.Tests.V1.E2ETests.Steps.Constants;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Infrastructure;
using ProcessesApi.V1.Infrastructure.JWT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ProcessesApi.Tests.V1.E2E.Steps
{
    public class UpdateSoleToJointProcessSteps : BaseSteps
    {
        private readonly IDynamoDbFixture _dbFixture;

        public UpdateSoleToJointProcessSteps(HttpClient httpClient, IDynamoDbFixture dbFixture) : base(httpClient)
        {
            _dbFixture = dbFixture;
        }

        public async Task WhenAnUpdateProcessRequestIsMade(UpdateProcessQuery request, UpdateProcessRequestObject requestBody, int? ifMatch)
        {
            var token = TestToken.Value;
            var uri = new Uri($"api/v1/process/{request.ProcessName}/{request.Id}/{request.ProcessTrigger}", UriKind.Relative);
            var message = new HttpRequestMessage(HttpMethod.Patch, uri);

            message.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            message.Headers.Add("Authorization", token);
            message.Headers.TryAddWithoutValidation(HeaderConstants.IfMatch, $"\"{ifMatch}\"");
            message.Method = HttpMethod.Patch;

            // Act
            _lastResponse = await _httpClient.SendAsync(message).ConfigureAwait(false);
        }

        public void ThenNotFoundIsReturned()
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        public void ThenInternalServerErrorIsReturned()
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.InternalServerError);

        }

        public async Task ThenVersionConflictExceptionIsReturned(int? ifMatch)
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.Conflict);

            var responseContent = await _lastResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var exception = string.Format("The version number supplied ({0}) does not match the current value on the entity (0).",
                                 (ifMatch is null) ? "{null}" : ifMatch.ToString());

            responseContent.Should().Contain(exception);
        }

        public void ThenBadRequestIsReturned()
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        public async Task ThenTheProcessDataIsUpdated(UpdateProcessQuery request, UpdateProcessRequestObject requestBody)
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var dbRecord = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(request.Id).ConfigureAwait(false);

            dbRecord.CurrentState.ProcessData.FormData.Should().HaveSameCount(requestBody.FormData); // workaround for comparing
            dbRecord.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(requestBody.Documents);
        }

        private async Task CheckProcessState(Guid processId, string currentState, string previousState)
        {
            var dbRecord = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(processId).ConfigureAwait(false);

            dbRecord.CurrentState.State.Should().Be(currentState);
            dbRecord.PreviousStates.Last().State.Should().Be(previousState);
        }

        public async Task ThenTheIncomingTenantIdIsAddedToRelatedEntities(UpdateProcessQuery request, UpdateProcessRequestObject requestBody, Person person)
        {
            var dbRecord = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(request.Id).ConfigureAwait(false);

            var incomingTenantId = Guid.Parse(requestBody.FormData[SoleToJointFormDataKeys.IncomingTenantId].ToString());
            var relatedEntity = dbRecord.RelatedEntities.Find(x => x.Id == incomingTenantId);
            relatedEntity.Should().NotBeNull();
            relatedEntity.TargetType.Should().Be(TargetType.person);
            relatedEntity.SubType.Should().Be(SubType.householdMember);
            relatedEntity.Description.Should().Be($"{person.FirstName} {person.Surname}");
        }

        public async Task ThenTheProcessStateIsUpdatedToProcessClosed(UpdateProcessQuery request, string previousState)
        {
            await CheckProcessState(request.Id, SharedProcessStates.ProcessClosed, previousState).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToProcessCancelled(UpdateProcessQuery request, string previousState)
        {
            await CheckProcessState(request.Id, SharedProcessStates.ProcessCancelled, previousState).ConfigureAwait(false);
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
            await CheckProcessState(request.Id, SoleToJointStates.DocumentsRequestedDes, SoleToJointStates.BreachChecksPassed).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToDocumentsRequestedAppointment(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.DocumentsRequestedAppointment, SoleToJointStates.BreachChecksPassed).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToDocumentsAppointmentRescheduled(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.DocumentsAppointmentRescheduled, SoleToJointStates.DocumentsRequestedAppointment).ConfigureAwait(false);
        }
        public async Task ThenTheProcessStateRemainsDocumentsAppointmentRescheduled(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.DocumentsAppointmentRescheduled, SoleToJointStates.DocumentsAppointmentRescheduled).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToDocumentChecksPassed(UpdateProcessQuery request, string initialState)
        {
            await CheckProcessState(request.Id, SoleToJointStates.DocumentChecksPassed, initialState).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToApplicationSubmitted(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.ApplicationSubmitted, SoleToJointStates.DocumentChecksPassed).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToShowResultsOfTenureInvestigation(UpdateProcessQuery request, string destinationState)
        {
            await CheckProcessState(request.Id, destinationState, SoleToJointStates.ApplicationSubmitted).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToInterviewScheduled(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.InterviewScheduled, SoleToJointStates.TenureInvestigationPassedWithInt).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToInterviewRescheduled(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.InterviewRescheduled, SoleToJointStates.InterviewScheduled).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateRemainsInterviewRescheduled(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.InterviewRescheduled, SoleToJointStates.InterviewRescheduled).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToShowResultsOfHOApproval(UpdateProcessQuery request, string destinationState, string initialState)
        {
            await CheckProcessState(request.Id, destinationState, initialState).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToScheduleTenureAppointment(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.TenureAppointmentScheduled, SoleToJointStates.HOApprovalPassed).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToRescheduleTenureAppointment(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.TenureAppointmentRescheduled, SoleToJointStates.TenureAppointmentScheduled).ConfigureAwait(false);
        }
        public async Task ThenTheProcessStateRemainsTenureAppointmentRescheduled(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SoleToJointStates.TenureAppointmentRescheduled, SoleToJointStates.TenureAppointmentRescheduled).ConfigureAwait(false);
        }
        public async Task ThenTheProcessStateIsUpdatedToUpdateTenure(UpdateProcessQuery request, string initialState)
        {
            await CheckProcessState(request.Id, SoleToJointStates.TenureUpdated, initialState).ConfigureAwait(false);
        }
        public async Task VerifyProcessUpdatedEventIsRaised(ISnsFixture snsFixture, Guid processId, string oldState, string newState, Action<string> verifyNewStateData = null)
        {
            Action<string, string> verifyData = (dataAsString, state) =>
            {
                var dataDic = JsonSerializer.Deserialize<Dictionary<string, object>>(dataAsString, _jsonOptions);
                dataDic["state"].ToString().Should().Be(state);
            };

            Action<EntityEventSns> verifyFunc = actual =>
            {
                actual.Id.Should().NotBeEmpty();
                actual.CorrelationId.Should().NotBeEmpty();
                actual.DateTime.Should().BeCloseTo(DateTime.UtcNow, 2000);
                actual.EntityId.Should().Be(processId);

                verifyData(actual.EventData.OldData.ToString(), oldState);
                verifyData(actual.EventData.NewData.ToString(), newState);
                verifyNewStateData?.Invoke(actual.EventData.NewData.ToString());

                actual.EventType.Should().Be(ProcessEventConstants.PROCESS_UPDATED_EVENT);
                actual.SourceDomain.Should().Be(ProcessEventConstants.SOURCE_DOMAIN);
                actual.SourceSystem.Should().Be(ProcessEventConstants.SOURCE_SYSTEM);
                actual.Version.Should().Be(ProcessEventConstants.V1_VERSION);

                actual.User.Email.Should().Be(TestToken.UserEmail);
                actual.User.Name.Should().Be(TestToken.UserName);
            };

            var snsVerifier = snsFixture.GetSnsEventVerifier<EntityEventSns>();
            var snsResult = await snsVerifier.VerifySnsEventRaised(verifyFunc);

            if (!snsResult && snsVerifier.LastException != null) throw snsVerifier.LastException;
        }

        public async Task ThenTheProcessUpdatedEventIsRaised(ISnsFixture snsFixture, Guid processId, string oldState, string newState)
        {
            await VerifyProcessUpdatedEventIsRaised(snsFixture, processId, oldState, newState).ConfigureAwait(false);
        }

        public async Task ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(ISnsFixture snsFixture, Guid processId, string oldState, string newState)
        {
            Action<string> verifyData = (dataAsString) =>
            {
                var dataDic = JsonSerializer.Deserialize<Dictionary<string, object>>(dataAsString, _jsonOptions);
                var stateData = JsonSerializer.Deserialize<Dictionary<string, object>>(dataDic["stateData"].ToString(), _jsonOptions);
                stateData.Should().ContainKey(SoleToJointFormDataKeys.AppointmentDateTime);
            };
            await VerifyProcessUpdatedEventIsRaised(snsFixture, processId, oldState, newState, verifyData).ConfigureAwait(false);
        }

        public async Task VerifyProcessClosedEventIsRaised(ISnsFixture snsFixture, Guid processId, Action<EventData> verifyStateData)
        {
            Action<EntityEventSns> verifyFunc = actual =>
            {
                actual.Id.Should().NotBeEmpty();
                actual.CorrelationId.Should().NotBeEmpty();
                actual.DateTime.Should().BeCloseTo(DateTime.UtcNow, 2000);
                actual.EntityId.Should().Be(processId);

                verifyStateData(actual.EventData);

                actual.EventType.Should().Be(ProcessEventConstants.PROCESS_CLOSED_EVENT);
                actual.SourceDomain.Should().Be(ProcessEventConstants.SOURCE_DOMAIN);
                actual.SourceSystem.Should().Be(ProcessEventConstants.SOURCE_SYSTEM);
                actual.Version.Should().Be(ProcessEventConstants.V1_VERSION);

                actual.User.Email.Should().Be(TestToken.UserEmail);
                actual.User.Name.Should().Be(TestToken.UserName);
            };

            var snsVerifier = snsFixture.GetSnsEventVerifier<EntityEventSns>();
            var snsResult = await snsVerifier.VerifySnsEventRaised(verifyFunc);

            if (!snsResult && snsVerifier.LastException != null) throw snsVerifier.LastException;
        }

        public async Task VerifyProcessCompletedEventIsRaised(ISnsFixture snsFixture, Guid processId, Action<EventData> verifyStateData)
        {
            Action<EntityEventSns> verifyFunc = actual =>
            {
                actual.Id.Should().NotBeEmpty();
                actual.CorrelationId.Should().NotBeEmpty();
                actual.DateTime.Should().BeCloseTo(DateTime.UtcNow, 2000);
                actual.EntityId.Should().Be(processId);

                verifyStateData(actual.EventData);

                actual.EventType.Should().Be(ProcessEventConstants.PROCESS_COMPLETED_EVENT);
                actual.SourceDomain.Should().Be(ProcessEventConstants.SOURCE_DOMAIN);
                actual.SourceSystem.Should().Be(ProcessEventConstants.SOURCE_SYSTEM);
                actual.Version.Should().Be(ProcessEventConstants.V1_VERSION);

                actual.User.Email.Should().Be(TestToken.UserEmail);
                actual.User.Name.Should().Be(TestToken.UserName);
            };

            var snsVerifier = snsFixture.GetSnsEventVerifier<EntityEventSns>();
            var snsResult = await snsVerifier.VerifySnsEventRaised(verifyFunc);

            if (!snsResult && snsVerifier.LastException != null) throw snsVerifier.LastException;
        }

        public async Task ThenTheProcessClosedEventIsRaisedWithoutReason(ISnsFixture snsFixture, Guid processId)
        {
            Action<EventData> verifyData = (eventData) =>
            {
                var newDataDic = JsonSerializer.Deserialize<Dictionary<string, object>>(eventData.NewData.ToString(), _jsonOptions);
                newDataDic["state"].ToString().Should().Be(SharedProcessStates.ProcessClosed);
            };

            await VerifyProcessClosedEventIsRaised(snsFixture, processId, verifyData).ConfigureAwait(false);
        }

        private async Task VerifyProcessClosedEventIsRaisedWithStateData(ISnsFixture snsFixture, Guid processId, string newState, string key)
        {
            Action<EventData> verifyData = (eventData) =>
            {
                var newDataDic = JsonSerializer.Deserialize<Dictionary<string, object>>(eventData.NewData.ToString(), _jsonOptions);
                newDataDic["state"].ToString().Should().Be(newState);

                var stateData = JsonSerializer.Deserialize<Dictionary<string, object>>(newDataDic["stateData"].ToString(), _jsonOptions);
                stateData.Should().ContainKey(key);
            };
            await VerifyProcessClosedEventIsRaised(snsFixture, processId, verifyData).ConfigureAwait(false);
        }

        private async Task VerifyProcessCompletedEventIsRaisedWithStateData(ISnsFixture snsFixture, Guid processId, string newState, string key)
        {
            Action<EventData> verifyData = (eventData) =>
            {
                var newDataDic = JsonSerializer.Deserialize<Dictionary<string, object>>(eventData.NewData.ToString(), _jsonOptions);
                newDataDic["state"].ToString().Should().Be(newState);

                var stateData = JsonSerializer.Deserialize<Dictionary<string, object>>(newDataDic["stateData"].ToString(), _jsonOptions);
                stateData.Should().ContainKey(key);
            };
            await VerifyProcessCompletedEventIsRaised(snsFixture, processId, verifyData).ConfigureAwait(false);
        }

        public async Task ThenTheProcessClosedEventIsRaisedWithReason(ISnsFixture snsFixture, Guid processId)
        {
            await VerifyProcessClosedEventIsRaisedWithStateData(snsFixture, processId, SharedProcessStates.ProcessClosed, SoleToJointFormDataKeys.Reason).ConfigureAwait(false);
        }

        public async Task ThenTheProcessClosedEventIsRaisedWithComment(ISnsFixture snsFixture, Guid processId)
        {
            await VerifyProcessClosedEventIsRaisedWithStateData(snsFixture, processId, SharedProcessStates.ProcessCancelled, SoleToJointFormDataKeys.Comment).ConfigureAwait(false);
        }

        public async Task ThenTheProcessCompletedEventIsRaised(ISnsFixture snsFixture, Guid processId)
        {
            await VerifyProcessCompletedEventIsRaisedWithStateData(snsFixture, processId, SoleToJointStates.TenureUpdated, SoleToJointFormDataKeys.Reason).ConfigureAwait(false);
        }

        public async Task ThenTheExistingTenureHasEnded(Process process)
        {
            var tenure = await _dbFixture.DynamoDbContext.LoadAsync<TenureInformationDb>(process.TargetId).ConfigureAwait(false);
            tenure.Id.Should().Be(process.TargetId);
            tenure.EndOfTenureDate.Should().BeCloseTo(DateTime.UtcNow, 2000);
        }

        public async Task ThenANewTenureIsCreated(Process process, Person person)
        {
            var tenure = await _dbFixture.DynamoDbContext.LoadAsync<TenureInformationDb>(process.RelatedEntities.FirstOrDefault().Id).ConfigureAwait(false);
            tenure.StartOfTenureDate.Should().BeCloseTo(DateTime.UtcNow, 2000);
            tenure.PaymentReference.Should().Be(person.Tenures.FirstOrDefault().PaymentReference);

            //TenuredAsset
            tenure.TenuredAsset.Id.Should().Be(person.Tenures.FirstOrDefault().Id);
            tenure.TenuredAsset.FullAddress.Should().Be(person.Tenures.FirstOrDefault().AssetFullAddress);
            tenure.TenuredAsset.Uprn.Should().Be(person.Tenures.FirstOrDefault().Uprn);
            tenure.TenuredAsset.PropertyReference.Should().Be(person.Tenures.FirstOrDefault().PropertyReference);

            //HouseholdMembers
            tenure.HouseholdMembers.FirstOrDefault().IsResponsible.Should().Be(true);
            tenure.HouseholdMembers.FirstOrDefault().FullName.Should().Be($"{person.FirstName} {person.Surname}");
            tenure.HouseholdMembers.FirstOrDefault().DateOfBirth.Should().Be((DateTime) person.DateOfBirth);
            tenure.HouseholdMembers.FirstOrDefault().Type.Should().Be(HouseholdMembersType.Person);
            tenure.HouseholdMembers.FirstOrDefault().PersonTenureType.Should().Be(person.PersonTypes.FirstOrDefault());


        }
    }
}
