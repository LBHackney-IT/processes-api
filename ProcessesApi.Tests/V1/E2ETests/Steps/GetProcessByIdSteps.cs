using FluentAssertions;
using Hackney.Core.Testing.Shared.E2E;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProcessesApi.Tests.V1.E2E.Steps
{
    public class GetProcessByIdSteps : BaseSteps
    {
        public GetProcessByIdSteps(HttpClient httpClient) : base(httpClient)
        {
        }

        public async Task WhenTheProcessIsRequested(string processName, string id)
        {
            var uri = new Uri($"api/v1/process/{processName}/{id}", UriKind.Relative);
            _lastResponse = await _httpClient.GetAsync(uri).ConfigureAwait(false);
        }

        public async Task ThenTheProcessIsReturned(Process process)
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.OK);

            var responseContent = await _lastResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
            var apiProcess = JsonSerializer.Deserialize<ProcessResponse>(responseContent, CreateJsonOptions());

            apiProcess.Should().BeEquivalentTo(process.ToDatabase(), config => config.Excluding(y => y.VersionNumber));

            var expectedEtagValue = $"\"{0}\"";
            _lastResponse.Headers.ETag.Tag.Should().Be(expectedEtagValue);
            var eTagHeaders = _lastResponse.Headers.GetValues(HeaderConstants.ETag);
            eTagHeaders.Count().Should().Be(1);
            eTagHeaders.First().Should().Be(expectedEtagValue);
        }

        public void ThenBadRequestIsReturned()
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        public void ThenNotFoundIsReturned()
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
