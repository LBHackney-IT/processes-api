using System;
using System.Threading.Tasks;
using Hackney.Core.Logging;
using Microsoft.Extensions.Logging;
using Hackney.Core.Http;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using ProcessesApi.V1.Boundary.Constants;
using Hackney.Shared.Tenure.Boundary.Requests;
using Hackney.Shared.Person.Boundary.Response;
using System.Text.Json;
using System.Text.Json.Serialization;
using JsonSerializer = System.Text.Json.JsonSerializer;
using Hackney.Shared.Tenure.Domain;

namespace ProcessesApi.V1.Gateways
{
    public class TenureApiGateway : ITenureApiGateway
    {
        private readonly ILogger<TenureApiGateway> _logger;
        private const string ApiName = "Tenure";
        private const string TenureApiUrl = "TenureApiUrl";
        private const string TenureApiToken = "TenureApiToken";
        private readonly IApiGateway _apiGateway;

        public TenureApiGateway(ILogger<TenureApiGateway> logger, IApiGateway apiGateway)
        {
            _logger = logger;
            _apiGateway = apiGateway;
            _apiGateway.Initialise(ApiName, TenureApiUrl, TenureApiToken, null);
        }

        private JsonSerializerOptions CreateJsonOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true
            };
            options.Converters.Add(new JsonStringEnumConverter());
            return options;
        }

        [LogCall]
        public async Task EditTenureDetailsById(Guid id, EditTenureDetailsRequestObject editTenureDetailsRequestObject, int? ifMatch)
        {
            _logger.LogDebug($"Calling Tenure API to update Tenure ID: {id}");

            var route = $"{_apiGateway.ApiRoute}/tenures/{id}";
            var uri = new Uri(route, UriKind.Absolute);
            var message = new HttpRequestMessage(HttpMethod.Patch, uri);

            var requestJson = JsonConvert.SerializeObject(editTenureDetailsRequestObject);
            message.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            message.Method = HttpMethod.Patch;
            _apiGateway.RequestHeaders.Add(HeaderConstants.IfMatch, $"\"{ifMatch?.ToString()}\"");

            await _apiGateway.SendAsync(message, Guid.NewGuid()).ConfigureAwait(false);
        }

        public async Task<TenureResponseObject> CreateNewTenure(CreateTenureRequestObject createTenureRequestObject)
        {
            _logger.LogDebug($"Calling Tenure API to create new tenure");

            var route = $"{_apiGateway.ApiRoute}/tenures";
            var uri = new Uri(route, UriKind.Absolute);
            var message = new HttpRequestMessage(HttpMethod.Post, uri);

            var requestJson = JsonConvert.SerializeObject(createTenureRequestObject);
            message.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
            message.Method = HttpMethod.Post;

            var response = await _apiGateway.SendAsync(message, Guid.NewGuid()).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            return JsonSerializer.Deserialize<TenureResponseObject>(responseContent, CreateJsonOptions());
        }

        public async Task UpdateTenureForPerson(Guid tenureId, Guid personId, UpdateTenureForPersonRequestObject requestObject, int? ifMatch)
        {
            _logger.LogDebug($"Calling Tenure API to update Tenure ID: {tenureId} for person: {personId}");

            var route = $"{_apiGateway.ApiRoute}/tenures/{tenureId}/person/{personId}";
            var uri = new Uri(route, UriKind.Absolute);
            var message = new HttpRequestMessage(HttpMethod.Patch, uri);

            var requestJson = JsonConvert.SerializeObject(requestObject);
            message.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            message.Method = HttpMethod.Patch;
            _apiGateway.RequestHeaders.Add(HeaderConstants.IfMatch, $"\"{ifMatch?.ToString()}\"");

            await _apiGateway.SendAsync(message, Guid.NewGuid()).ConfigureAwait(false);
        }
    }
}
