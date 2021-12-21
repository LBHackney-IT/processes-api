using Amazon.DynamoDBv2.DataModel;
using FluentAssertions;
using Hackney.Core.Testing.Shared.E2E;
using Newtonsoft.Json;
using ProcessesApi.V1.Boundary.Constants;
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

namespace ProcessesApi.Tests.V1.E2E.Steps
{
    public class CreateNewSoleToJointProcessSteps : BaseSteps
    {
        public CreateNewSoleToJointProcessSteps(HttpClient httpClient) : base(httpClient)
        {
        }

        public async Task WhenACreateProcessRequestIsMade(CreateProcess request, string processName)
        {
            var uri = new Uri($"api/v1/process/{processName}/", UriKind.Relative);
            var message = new HttpRequestMessage(HttpMethod.Post, uri);
            message.Content = new StringContent(JsonConvert.SerializeObject(request), Encoding.UTF8, "application/json");
            message.Method = HttpMethod.Post;

            // Act
            _lastResponse = await _httpClient.SendAsync(message).ConfigureAwait(false);
        }

        public async Task ThenTheProcessIsCreated(CreateProcess request, IDynamoDBContext dynamoDbContext)
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.Created);
            var responseContent = await _lastResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var apiProcess = JsonConvert.DeserializeObject<ProcessResponse>(responseContent);

            apiProcess.Id.Should().NotBeEmpty();
            var dbRecord = await dynamoDbContext.LoadAsync<ProcessesDb>(apiProcess.Id).ConfigureAwait(false);

            dbRecord.TargetId.Should().Be(request.TargetId);
            dbRecord.RelatedEntities.Should().BeEquivalentTo(request.RelatedEntities);
            dbRecord.ProcessName.Should().Be(ProcessNamesConstants.SoleToJoint);

            dbRecord.CurrentState.State.Should().Be(SoleToJointStates.SelectTenants);
            dbRecord.CurrentState.PermittedTriggers.Should().BeEquivalentTo(new List<string>() { SoleToJointPermittedTriggers.CheckEligibility });
            // TODO: Add test for assignment when implemented
            dbRecord.CurrentState.ProcessData.FormData.Should().BeEquivalentTo(request.FormData);
            dbRecord.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(request.Documents);
            dbRecord.CurrentState.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            dbRecord.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);

            dbRecord.PreviousStates.Should().BeEmpty();

            // Cleanup
            await dynamoDbContext.DeleteAsync<ProcessesDb>(dbRecord.Id).ConfigureAwait(false);
        }

        public void ThenBadRequestIsReturned()
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }
    }
}
