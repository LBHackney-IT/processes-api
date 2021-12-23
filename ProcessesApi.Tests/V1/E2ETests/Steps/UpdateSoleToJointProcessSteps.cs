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
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ProcessesApi.Tests.V1.E2E.Steps
{
    public class UpdateSoleToJointProcessSteps : BaseSteps
    {
        public UpdateSoleToJointProcessSteps(HttpClient httpClient) : base(httpClient)
        {
        }

        public async Task WhenAnUpdateProcessRequestIsMade(UpdateProcessQuery request, UpdateProcessQueryObject requestBody)
        {
            var uri = new Uri($"api/v1/process/{request.ProcessName}/{request.Id}/{request.ProcessTrigger}", UriKind.Relative);
            var message = new HttpRequestMessage(HttpMethod.Patch, uri);
            message.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
            message.Headers.TryAddWithoutValidation(HeaderConstants.IfMatch, $"\"{0}\"");
            message.Method = HttpMethod.Patch;

            // Act
            _lastResponse = await _httpClient.SendAsync(message).ConfigureAwait(false);
        }

        public void ThenNotFoundIsReturned()
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }

        public async Task ThenTheProcessStateIsUpdatedToEligibilityChecksPassed(UpdateProcessQuery request, UpdateProcessQueryObject requestBody, IDynamoDBContext dynamoDbContext)
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var dbRecord = await dynamoDbContext.LoadAsync<ProcessesDb>(request.Id).ConfigureAwait(false);

            var incomingTenantId = Guid.Parse(requestBody.FormData[SoleToJointFormDataKeys.IncomingTenantId].ToString());
            dbRecord.RelatedEntities.Should().Contain(incomingTenantId);

            dbRecord.CurrentState.State.Should().Be(SoleToJointStates.AutomatedChecksPassed);
            dbRecord.CurrentState.ProcessData.FormData.Should().BeEquivalentTo(requestBody.FormData);
            dbRecord.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(requestBody.Documents);
            // TODO when implementing next state: Add check for permittedTriggers

            dbRecord.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.SelectTenants);
        }
    }
}
