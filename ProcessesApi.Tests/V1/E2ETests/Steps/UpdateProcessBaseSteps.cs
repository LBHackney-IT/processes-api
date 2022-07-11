using FluentAssertions;
using Hackney.Core.Sns;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Shared.E2E;
using Hackney.Core.Testing.Sns;
using Newtonsoft.Json;
using ProcessesApi.Tests.V1.E2ETests.Steps.Constants;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Infrastructure;
using ProcessesApi.V1.Infrastructure.JWT;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using JsonSerializer = System.Text.Json.JsonSerializer;

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

        public async Task CheckProcessState(Guid processId, string currentState, string previousState)
        {
            var dbRecord = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(processId).ConfigureAwait(false);

            dbRecord.CurrentState.State.Should().Be(currentState);
            dbRecord.PreviousStates.Last().State.Should().Be(previousState);
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
    }
}
