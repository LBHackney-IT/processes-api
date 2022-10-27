using FluentAssertions;
using Hackney.Core.Sns;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Shared.E2E;
using Hackney.Core.Testing.Sns;
using Newtonsoft.Json;
using ProcessesApi.Tests.V1.E2ETests.Steps.Constants;
using Hackney.Shared.Processes.Boundary.Constants;
using Hackney.Shared.Processes.Boundary.Request;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Hackney.Shared.Processes.Domain.Constants;
using JsonSerializer = System.Text.Json.JsonSerializer;
using SharedKeys = Hackney.Shared.Processes.Domain.Constants.SharedKeys;
using Hackney.Shared.Processes.Sns;
using ProcessesApi.V1.Infrastructure;

namespace ProcessesApi.Tests.V1.E2ETests.Steps
{
    public class UpdateProcessBaseSteps : BaseSteps
    {
        private readonly IDynamoDbFixture _dbFixture;

        public UpdateProcessBaseSteps(HttpClient httpClient, IDynamoDbFixture dbFixture) : base(httpClient)
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

        # region Status Codes

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

        # endregion

        # region Process Data

        public async Task ThenTheProcessDataIsUpdated(UpdateProcessQuery request, UpdateProcessRequestObject requestBody)
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var dbRecord = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(request.Id).ConfigureAwait(false);

            dbRecord.CurrentState.ProcessData.FormData.Should().HaveSameCount(requestBody.FormData); // workaround for comparing
            dbRecord.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(requestBody.Documents);
        }

        public async Task CheckProcessState(Guid processId, string currentState, string previousState)
        {
            var dbRecord = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(processId).ConfigureAwait(false);

            dbRecord.CurrentState.State.Should().Be(currentState);
            dbRecord.PreviousStates.Last().State.Should().Be(previousState);
        }

        public async Task ThenTheProcessStateIsUpdatedToProcessClosed(UpdateProcessQuery request, string previousState)
        {
            await CheckProcessState(request.Id, SharedStates.ProcessClosed, previousState).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToProcessCompleted(UpdateProcessQuery request, string previousState)
        {
            await CheckProcessState(request.Id, SharedStates.ProcessCompleted, previousState).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToDocumentsAppointmentRescheduled(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SharedStates.DocumentsAppointmentRescheduled, SharedStates.DocumentsRequestedAppointment).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateRemainsDocumentsAppointmentRescheduled(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SharedStates.DocumentsAppointmentRescheduled, SharedStates.DocumentsAppointmentRescheduled).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToProcessCancelled(UpdateProcessQuery request, string previousState)
        {
            await CheckProcessState(request.Id, SharedStates.ProcessCancelled, previousState).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToDocumentChecksPassed(UpdateProcessQuery request, string initialState)
        {
            await CheckProcessState(request.Id, SharedStates.DocumentChecksPassed, initialState).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToApplicationSubmitted(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SharedStates.ApplicationSubmitted, SharedStates.DocumentChecksPassed).ConfigureAwait(false);
        }
        public async Task ThenTheProcessStateIsUpdatedToShowResultsOfTenureInvestigation(UpdateProcessQuery request, string destinationState)
        {
            await CheckProcessState(request.Id, destinationState, SharedStates.ApplicationSubmitted).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToShowResultsOfHOApproval(UpdateProcessQuery request, string destinationState, string initialState)
        {
            await CheckProcessState(request.Id, destinationState, initialState).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToInterviewScheduled(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SharedStates.InterviewScheduled, SharedStates.TenureInvestigationPassedWithInt).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToInterviewRescheduled(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SharedStates.InterviewRescheduled, SharedStates.InterviewScheduled).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateRemainsInterviewRescheduled(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SharedStates.InterviewRescheduled, SharedStates.InterviewRescheduled).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToScheduleTenureAppointment(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SharedStates.TenureAppointmentScheduled, SharedStates.HOApprovalPassed).ConfigureAwait(false);
        }

        public async Task ThenTheProcessStateIsUpdatedToRescheduleTenureAppointment(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SharedStates.TenureAppointmentRescheduled, SharedStates.TenureAppointmentScheduled).ConfigureAwait(false);
        }
        public async Task ThenTheProcessStateRemainsTenureAppointmentRescheduled(UpdateProcessQuery request)
        {
            await CheckProcessState(request.Id, SharedStates.TenureAppointmentRescheduled, SharedStates.TenureAppointmentRescheduled).ConfigureAwait(false);
        }


        # endregion

        # region Events

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
                actual.DateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMilliseconds(2000));
                actual.EntityId.Should().Be(processId);

                verifyData(actual.EventData.OldData.ToString(), oldState);
                verifyData(actual.EventData.NewData.ToString(), newState);
                verifyNewStateData?.Invoke(actual.EventData.NewData.ToString());

                actual.EventType.Should().Be(EventConstants.PROCESS_UPDATED_EVENT);
                actual.SourceDomain.Should().Be(EventConstants.SOURCE_DOMAIN);
                actual.SourceSystem.Should().Be(EventConstants.SOURCE_SYSTEM);
                actual.Version.Should().Be(EventConstants.V1_VERSION);

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
            // todo figure out how to verify other events e.g. tenure updated
        }

        public async Task ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(ISnsFixture snsFixture, Guid processId, string oldState, string newState)
        {
            Action<string> verifyData = (dataAsString) =>
            {
                var dataDic = JsonSerializer.Deserialize<Dictionary<string, object>>(dataAsString, _jsonOptions);
                var stateData = JsonSerializer.Deserialize<Dictionary<string, object>>(dataDic["stateData"].ToString(), _jsonOptions);
                stateData.Should().ContainKey(SharedKeys.AppointmentDateTime);
            };

            await VerifyProcessUpdatedEventIsRaised(snsFixture, processId, oldState, newState, verifyData).ConfigureAwait(false);
        }

        public async Task ThenTheProcessUpdatedEventIsRaisedWithHOApprovalDetails(ISnsFixture snsFixture, Guid processId, UpdateProcessRequestObject requestObject, string oldState, string newState)
        {
            Action<string> verifyData = (dataAsString) =>
            {
                var dataDic = JsonSerializer.Deserialize<Dictionary<string, object>>(dataAsString, _jsonOptions);
                var stateData = JsonSerializer.Deserialize<Dictionary<string, object>>(dataDic["stateData"].ToString(), _jsonOptions);
                stateData.Should().ContainKey(SharedKeys.HousingAreaManagerName);
                if (requestObject.FormData.ContainsKey(SharedKeys.Reason)) stateData.Should().ContainKey(SharedKeys.Reason);
            };

            await VerifyProcessUpdatedEventIsRaised(snsFixture, processId, oldState, newState, verifyData).ConfigureAwait(false);
        }

        public async Task VerifyProcessClosedEventIsRaised(ISnsFixture snsFixture, Guid processId, string oldState, string newState, Action<string> verifyNewStateData = null)
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
                actual.DateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMilliseconds(2000));
                actual.EntityId.Should().Be(processId);

                verifyData(actual.EventData.OldData.ToString(), oldState);
                verifyData(actual.EventData.NewData.ToString(), newState);
                verifyNewStateData?.Invoke(actual.EventData.NewData.ToString());

                actual.EventType.Should().Be(EventConstants.PROCESS_CLOSED_EVENT);
                actual.SourceDomain.Should().Be(EventConstants.SOURCE_DOMAIN);
                actual.SourceSystem.Should().Be(EventConstants.SOURCE_SYSTEM);
                actual.Version.Should().Be(EventConstants.V1_VERSION);

                actual.User.Email.Should().Be(TestToken.UserEmail);
                actual.User.Name.Should().Be(TestToken.UserName);
            };

            var snsVerifier = snsFixture.GetSnsEventVerifier<EntityEventSns>();
            var snsResult = await snsVerifier.VerifySnsEventRaised(verifyFunc);

            if (!snsResult && snsVerifier.LastException != null) throw snsVerifier.LastException;
        }

        public async Task ThenTheProcessClosedEventIsRaised(ISnsFixture snsFixture, Guid processId, UpdateProcessRequestObject requestObject, string oldState)
        {
            Action<EventData> verifyData = (eventData) =>
            {
                var newDataDic = JsonSerializer.Deserialize<Dictionary<string, object>>(eventData.NewData.ToString(), _jsonOptions);
                var stateData = JsonSerializer.Deserialize<Dictionary<string, object>>(newDataDic["stateData"].ToString(), _jsonOptions);
                if (requestObject.FormData.ContainsKey(SharedKeys.Reason)) stateData.Should().ContainKey(SharedKeys.Reason);
            };

            await VerifyProcessClosedEventIsRaised(snsFixture, processId, oldState, SharedStates.ProcessClosed).ConfigureAwait(false);
        }

        public async Task ThenTheProcessClosedEventIsRaisedWithComment(ISnsFixture snsFixture, Guid processId, string oldState)
        {
            Action<EventData> verifyData = (eventData) =>
            {
                var newDataDic = JsonSerializer.Deserialize<Dictionary<string, object>>(eventData.NewData.ToString(), _jsonOptions);
                var stateData = JsonSerializer.Deserialize<Dictionary<string, object>>(newDataDic["stateData"].ToString(), _jsonOptions);
                stateData.Should().ContainKey(SharedKeys.Comment);
            };

            await VerifyProcessClosedEventIsRaised(snsFixture, processId, oldState, SharedStates.ProcessCancelled).ConfigureAwait(false);
        }

        public async Task VerifyProcessCompletedEventIsRaised(ISnsFixture snsFixture, Guid processId, string oldState, string newState, Action<string> verifyNewStateData = null)
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
                actual.DateTime.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMilliseconds(2000));
                actual.EntityId.Should().Be(processId);

                verifyData(actual.EventData.OldData.ToString(), oldState);
                verifyData(actual.EventData.NewData.ToString(), newState);
                verifyNewStateData?.Invoke(actual.EventData.NewData.ToString());

                actual.EventType.Should().Be(EventConstants.PROCESS_COMPLETED_EVENT);
                actual.SourceDomain.Should().Be(EventConstants.SOURCE_DOMAIN);
                actual.SourceSystem.Should().Be(EventConstants.SOURCE_SYSTEM);
                actual.Version.Should().Be(EventConstants.V1_VERSION);

                actual.User.Email.Should().Be(TestToken.UserEmail);
                actual.User.Name.Should().Be(TestToken.UserName);
            };

            var snsVerifier = snsFixture.GetSnsEventVerifier<EntityEventSns>();
            var snsResult = await snsVerifier.VerifySnsEventRaised(verifyFunc);

            if (!snsResult && snsVerifier.LastException != null) throw snsVerifier.LastException;
        }

        public async Task ThenTheProcessCompletedEventIsRaised(ISnsFixture snsFixture, Guid processId, string oldState, string newState)
        {
            await VerifyProcessCompletedEventIsRaised(snsFixture, processId, oldState, newState).ConfigureAwait(false);
        }
        # endregion
    }
}
