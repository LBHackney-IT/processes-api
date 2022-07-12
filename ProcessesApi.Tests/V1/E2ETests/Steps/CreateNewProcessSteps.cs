using FluentAssertions;
using Hackney.Core.Testing.Shared.E2E;
using Newtonsoft.Json;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Infrastructure;
using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Hackney.Core.Testing.DynamoDb;
using ProcessesApi.Tests.V1.E2E.Fixtures;
using Hackney.Core.Testing.Sns;
using Hackney.Core.Sns;
using ProcessesApi.Tests.V1.E2ETests.Steps.Constants;
using ProcessesApi.V1.Infrastructure.JWT;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Constants.SoleToJoint;
using ProcessesApi.V1.Constants.ChangeOfName;

namespace ProcessesApi.Tests.V1.E2E.Steps
{
    public class CreateNewProcessSteps : BaseSteps
    {
        private readonly IDynamoDbFixture _dbFixture;

        public CreateNewProcessSteps(HttpClient httpClient, IDynamoDbFixture dbFixture) : base(httpClient)
        {
            _dbFixture = dbFixture;
        }

        public async Task WhenACreateProcessRequestIsMade(CreateProcess request, ProcessName processName)
        {
            var token = TestToken.Value;
            var uri = new Uri($"api/v1/process/{processName}/", UriKind.Relative);
            var message = new HttpRequestMessage(HttpMethod.Post, uri);
            message.Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            message.Headers.Add("Authorization", token);
            message.Method = HttpMethod.Post;

            // Act
            _lastResponse = await _httpClient.SendAsync(message).ConfigureAwait(false);
        }

        public async Task ThenTheProcessIsCreated(CreateProcess request, ProcessName processName, string state, List<string> permittedTriggers)
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var responseContent = await _lastResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var apiProcess = JsonConvert.DeserializeObject<ProcessResponse>(responseContent);

            apiProcess.Id.Should().NotBeEmpty();
            var dbRecord = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(apiProcess.Id).ConfigureAwait(false);

            dbRecord.TargetId.Should().Be(request.TargetId);
            dbRecord.RelatedEntities.Should().BeEquivalentTo(request.RelatedEntities);
            dbRecord.ProcessName.Should().Be(processName);

            dbRecord.CurrentState.State.Should().Be(state);
            dbRecord.CurrentState.PermittedTriggers.Should().BeEquivalentTo(permittedTriggers);
            // TODO: Add test for assignment when implemented
            dbRecord.CurrentState.ProcessData.FormData.Should().BeEquivalentTo(request.FormData);
            dbRecord.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(request.Documents);
            dbRecord.CurrentState.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            dbRecord.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);

            dbRecord.PreviousStates.Should().BeEmpty();

            // Cleanup
            await _dbFixture.DynamoDbContext.DeleteAsync<ProcessesDb>(dbRecord.Id).ConfigureAwait(false);
        }

        public async Task ThenTheSoleToJointProcessIsCreated(CreateProcess request)
        {
            await ThenTheProcessIsCreated(request, ProcessName.soletojoint, SoleToJointStates.SelectTenants, new List<string>() { SoleToJointPermittedTriggers.CheckAutomatedEligibility }).ConfigureAwait(false);
        }

        public async Task ThenTheChangeOfNameProcessIsCreated(CreateProcess request)
        {
            await ThenTheProcessIsCreated(request, ProcessName.changeofname, ChangeOfNameStates.EnterNewName, new List<string>() { ChangeOfNamePermittedTriggers.EnterNewName }).ConfigureAwait(false);
        }


        public async Task ThenProcessStartedEventIsRaised(ProcessFixture processFixture, ISnsFixture snsFixture)
        {
            var responseContent = await _lastResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var apiProcess = JsonConvert.DeserializeObject<ProcessResponse>(responseContent);

            apiProcess.Id.Should().NotBeEmpty();
            var dbRecord = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(apiProcess.Id).ConfigureAwait(false);

            Action<EntityEventSns> verifyFunc = (actual) =>
            {
                actual.CorrelationId.Should().NotBeEmpty();
                actual.DateTime.Should().BeCloseTo(DateTime.UtcNow, 2000);
                actual.EntityId.Should().Be(dbRecord.Id);

                var expected = dbRecord.ToDomain();
                var actualNewData = JsonConvert.DeserializeObject<Process>(actual.EventData.NewData.ToString());
                actualNewData.Should().BeEquivalentTo(expected, c => c.Excluding(x => x.CurrentState)
                                                                      .Excluding(x => x.VersionNumber));

                actual.EventData.OldData.Should().BeNull();

                actual.EventType.Should().Be(ProcessEventConstants.PROCESS_STARTED_EVENT);
                actual.Id.Should().NotBeEmpty();
                actual.SourceDomain.Should().Be(ProcessEventConstants.SOURCE_DOMAIN);
                actual.SourceSystem.Should().Be(ProcessEventConstants.SOURCE_SYSTEM);
                actual.User.Email.Should().Be(TestToken.UserEmail);
                actual.User.Name.Should().Be(TestToken.UserName);
                actual.Version.Should().Be(ProcessEventConstants.V1_VERSION);
            };

            var snsVerifier = snsFixture.GetSnsEventVerifier<EntityEventSns>();
            var snsResult = await snsVerifier.VerifySnsEventRaised(verifyFunc);

            if (!snsResult && snsVerifier.LastException != null) throw snsVerifier.LastException;
        }

        public void ThenBadRequestIsReturned()
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
