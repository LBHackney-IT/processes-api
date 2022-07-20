using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using FluentAssertions;
using Hackney.Core.DynamoDb;
using Hackney.Core.Testing.Shared.E2E;
using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Infrastructure;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ProcessesApi.Tests.V1.E2E.Steps
{
    public class GetProcessesByTargetIdSteps : BaseSteps
    {
        private readonly List<ProcessResponse> _pagedProcesses = new List<ProcessResponse>();

        public GetProcessesByTargetIdSteps(HttpClient httpClient) : base(httpClient)
        { }

        private async Task<HttpResponseMessage> CallApi(Guid id, string paginationToken = null, int? pageSize = null)
        {
            var route = $"api/v1/process?targetId={id}";
            if (!string.IsNullOrEmpty(paginationToken))
                route += $"&paginationToken={paginationToken}";
            if (pageSize.HasValue)
                route += $"&pageSize={pageSize.Value}";
            var uri = new Uri(route, UriKind.Relative);
            return await _httpClient.GetAsync(uri).ConfigureAwait(false);
        }

        private async Task<PagedResult<ProcessResponse>> ExtractResultFromHttpResponse(HttpResponseMessage response)
        {
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var apiResult = JsonSerializer.Deserialize<PagedResult<ProcessResponse>>(responseContent, CreateJsonOptions());
            return apiResult;
        }

        #region When

        public async Task WhenTheTargetProcessesAreRequested(Guid id)
        {
            _lastResponse = await CallApi(id).ConfigureAwait(false);
        }

        public async Task WhenTheTargetProcessesAreRequestedWithPageSize(Guid id, int? pageSize = null)
        {
            _lastResponse = await CallApi(id, null, pageSize).ConfigureAwait(false);
        }

        public async Task WhenAllTheTargetProcessesAreRequested(Guid id)
        {
            string pageToken = null;
            do
            {
                var response = await CallApi(id, pageToken).ConfigureAwait(false);
                var apiResult = await ExtractResultFromHttpResponse(response).ConfigureAwait(false);
                _pagedProcesses.AddRange(apiResult.Results);

                pageToken = apiResult.PaginationDetails.NextToken;
            }
            while (!string.IsNullOrEmpty(pageToken));
        }

        #endregion

        #region Then

        public async Task ThenTheTargetProcessesAreReturned(List<ProcessesDb> expectedProcesses)
        {
            var apiResult = await ExtractResultFromHttpResponse(_lastResponse).ConfigureAwait(false);
            apiResult.Results.Should().BeEquivalentTo(expectedProcesses, c => c.Excluding(x => x.VersionNumber));
        }

        public async Task ThenAllTheTargetProcessesAreReturnedWithNoPaginationToken(List<ProcessesDb> expectedProcesses)
        {
            var apiResult = await ExtractResultFromHttpResponse(_lastResponse).ConfigureAwait(false);
            apiResult.Results.Should().BeEquivalentTo(expectedProcesses, c => c.Excluding(x => x.VersionNumber));

            apiResult.PaginationDetails.HasNext.Should().BeFalse();
            apiResult.PaginationDetails.NextToken.Should().BeNull();
        }

        public async Task ThenTheTargetProcessesAreReturnedByPageSize(List<ProcessesDb> expectedProcesses, int? pageSize)
        {
            var expectedPageSize = 10;
            if (pageSize.HasValue)
                expectedPageSize = (pageSize.Value > expectedProcesses.Count) ? expectedProcesses.Count : pageSize.Value;

            var apiResult = await ExtractResultFromHttpResponse(_lastResponse).ConfigureAwait(false);
            apiResult.Results.Count.Should().Be(expectedPageSize);
            apiResult.Results.Should().BeEquivalentTo(expectedProcesses.OrderBy(x => x.Id).Take(expectedPageSize),
                                                      c => c.Excluding(x => x.VersionNumber));
        }

        public async Task ThenTheFirstPageOfTargetProcessesAreReturned(List<ProcessesDb> expectedProcesses)
        {
            var apiResult = await ExtractResultFromHttpResponse(_lastResponse).ConfigureAwait(false);
            apiResult.PaginationDetails.NextToken.Should().NotBeNullOrEmpty();
            apiResult.Results.Count.Should().Be(10);
            apiResult.Results.Should().BeEquivalentTo(expectedProcesses.OrderBy(x => x.Id).Take(10),
                                                      c => c.Excluding(x => x.VersionNumber));
        }

        public void ThenAllTheTargetProcessesAreReturned(List<ProcessesDb> expectedProcesses)
        {
            _pagedProcesses.Should().BeEquivalentTo(expectedProcesses.OrderBy(x => x.Id),
                                                    c => c.Excluding(x => x.VersionNumber));
        }

        public void ThenBadRequestIsReturned()
        {
            _lastResponse.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        #endregion
    }
}
