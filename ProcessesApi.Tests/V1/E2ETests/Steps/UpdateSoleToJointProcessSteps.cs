using Amazon.DynamoDBv2.DataModel;
using FluentAssertions;
using Hackney.Core.Testing.Shared.E2E;
using Newtonsoft.Json;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Infrastructure;
using System;
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

        public async Task WhenAnUpdateProcessRequestIsMade(UpdateProcessQuery request, UpdateProcessQueryObject requestBody, int? ifMatch)
        {
            var uri = new Uri($"api/v1/process/{request.ProcessName}/{request.Id}/{request.ProcessTrigger}", UriKind.Relative);
            var message = new HttpRequestMessage(HttpMethod.Patch, uri);
            message.Content = new StringContent(JsonConvert.SerializeObject(requestBody), Encoding.UTF8, "application/json");
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

        public async Task ThenTheProcessDataIsUpdated(UpdateProcessQuery request, UpdateProcessQueryObject requestBody, IDynamoDBContext dynamoDbContext)
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var dbRecord = await dynamoDbContext.LoadAsync<ProcessesDb>(request.Id).ConfigureAwait(false);

            var incomingTenantId = Guid.Parse(requestBody.FormData[SoleToJointFormDataKeys.IncomingTenantId].ToString());
            dbRecord.RelatedEntities.Should().Contain(incomingTenantId);

            dbRecord.CurrentState.ProcessData.FormData.Should().HaveSameCount(requestBody.FormData); // workaround for comparing
            dbRecord.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(requestBody.Documents);
            // TODO when implementing next state: Add check for permittedTriggers
        }

        public async Task AndTheProcessStateIsUpdatedToEligibilityChecksPassed(UpdateProcessQuery request, UpdateProcessQueryObject requestBody, IDynamoDBContext dynamoDbContext)
        {
            var dbRecord = await dynamoDbContext.LoadAsync<ProcessesDb>(request.Id).ConfigureAwait(false);

            dbRecord.CurrentState.State.Should().Be(SoleToJointStates.AutomatedChecksPassed);
            dbRecord.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.SelectTenants);
        }

        public async Task AndTheProcessStateIsUpdatedToEligibilityChecksFailed(UpdateProcessQuery request, UpdateProcessQueryObject requestBody, IDynamoDBContext dynamoDbContext)
        {
            var dbRecord = await dynamoDbContext.LoadAsync<ProcessesDb>(request.Id).ConfigureAwait(false);

            dbRecord.CurrentState.State.Should().Be(SoleToJointStates.AutomatedChecksFailed);
            dbRecord.PreviousStates.LastOrDefault().State.Should().Be(SoleToJointStates.SelectTenants);
        }
    }
}
