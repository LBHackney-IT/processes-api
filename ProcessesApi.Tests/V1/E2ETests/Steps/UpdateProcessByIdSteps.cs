using FluentAssertions;
using Hackney.Core.Sns;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Shared.E2E;
using Hackney.Core.Testing.Sns;
using Newtonsoft.Json;
using ProcessesApi.Tests.V1.E2E.Fixtures;
using ProcessesApi.Tests.V1.E2ETests.Steps.Constants;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure;
using ProcessesApi.V1.Infrastructure.JWT;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ProcessesApi.Tests.V1.E2ETests.Steps
{
    public class UpdateProcessByIdSteps : BaseSteps
    {
        private readonly IDynamoDbFixture _dbFixture;
        public UpdateProcessByIdSteps(HttpClient httpClient, IDynamoDbFixture dbFixture) : base(httpClient)
        {
            _dbFixture = dbFixture;
        }

        public async Task WhenAnUpdateProcessByIdRequestIsMade(ProcessQuery request, UpdateProcessByIdRequestObject requestObject, int? ifMatch)
        {
            var token = TestToken.Value;
            var uri = new Uri($"api/v1/process/{request.ProcessName}/{request.Id}", UriKind.Relative);
            var message = new HttpRequestMessage(HttpMethod.Patch, uri);

            message.Content = new StringContent(JsonConvert.SerializeObject(requestObject), Encoding.UTF8, "application/json");
            message.Headers.Add("Authorization", token);
            message.Headers.TryAddWithoutValidation(HeaderConstants.IfMatch, $"\"{ifMatch}\"");
            message.Method = HttpMethod.Patch;


            _lastResponse = await _httpClient.SendAsync(message).ConfigureAwait(false);
        }

        public async Task ThenTheProcessUpdatedEventIsRaised(ISnsFixture snsFixture, ProcessFixture processFixture)
        {
            var dbEntity = await processFixture._dbContext.LoadAsync<ProcessesDb>(processFixture.ProcessId).ConfigureAwait(false);

            Action<string, ProcessesDb> verifyData = (dataAsString, process) =>
            {
                var dataDic = JsonSerializer.Deserialize<Dictionary<string, object>>(dataAsString, CreateJsonOptions());
                dataDic["processData"].Should().Be(process.CurrentState.ProcessData);
                dataDic["assignment"].Should().Be(process.CurrentState.Assignment);
            };

            Action<EntityEventSns> verifyFunc = actual =>
            {
                actual.Id.Should().NotBeEmpty();
                actual.CorrelationId.Should().NotBeEmpty();
                actual.DateTime.Should().BeCloseTo(DateTime.UtcNow, 2000);
                actual.EntityId.Should().Be(processFixture.ProcessId);

                verifyData(actual.EventData.OldData.ToString(), processFixture.Process.ToDatabase());
                verifyData(actual.EventData.NewData.ToString(), dbEntity);

                actual.EventType.Should().Be(ProcessUpdatedEventConstants.EVENTTYPE);
                actual.SourceDomain.Should().Be(ProcessUpdatedEventConstants.SOURCE_DOMAIN);
                actual.SourceSystem.Should().Be(ProcessUpdatedEventConstants.SOURCE_SYSTEM);
                actual.Version.Should().Be(ProcessUpdatedEventConstants.V1_VERSION);

                actual.User.Email.Should().Be(TestToken.UserEmail);
                actual.User.Name.Should().Be(TestToken.UserName);
            };

            var snsVerifier = snsFixture.GetSnsEventVerifier<EntityEventSns>();
            var snsResult = await snsVerifier.VerifySnsEventRaised(verifyFunc);

            if (!snsResult && snsVerifier.LastException != null) throw snsVerifier.LastException;
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

        public async Task ThenTheProcessDataIsUpdated(ProcessQuery request, UpdateProcessByIdRequestObject requestBody)
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var dbRecord = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(request.Id).ConfigureAwait(false);

            dbRecord.Id.Should().Be(request.Id);
            dbRecord.CurrentState.ProcessData.FormData.Should().BeEquivalentTo(requestBody.ProcessData.FormData);
            dbRecord.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(requestBody.ProcessData.Documents);
            dbRecord.CurrentState.Assignment.Should().BeEquivalentTo(requestBody.Assignment);
            dbRecord.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
        }
    }
}