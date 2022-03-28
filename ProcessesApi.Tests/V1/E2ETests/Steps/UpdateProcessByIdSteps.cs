using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Shared.E2E;
using Newtonsoft.Json;
using ProcessesApi.Tests.V1.E2ETests.Steps.Constants;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Infrastructure;
using System;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;

namespace ProcessesApi.Tests.V1.E2ETests.Steps
{
    public class UpdateProcessByIdSteps : BaseSteps
    {
        private readonly IDynamoDbFixture _dbFixture;
        public UpdateProcessByIdSteps(HttpClient httpClient, IDynamoDbFixture dbFixture) : base(httpClient)
        {
            _dbFixture = dbFixture;
        }

        public async Task WhenAnUpdateProcessByIdRequestIsMade(ProcessQuery request, UpdateProcessByIdRequestObject requestBody, int? ifMatch)
        {
            var token = TestToken.Value;
            var uri = new Uri($"api/v1/process/{request.ProcessName}/{request.Id}", UriKind.Relative);
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

        public async Task ThenTheProcessDataIsUpdated(ProcessQuery request, UpdateProcessByIdRequestObject requestBody)
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var dbRecord = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(request.Id).ConfigureAwait(false);

            dbRecord.Id.Should().Be(request.Id);
            dbRecord.CurrentState.ProcessData.FormData.Should().BeEquivalentTo(requestBody.FormData);
            dbRecord.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(requestBody.Documents);
            dbRecord.CurrentState.Assignment.Should().BeEquivalentTo(requestBody.Assignment);
            dbRecord.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
        }
    }
}
