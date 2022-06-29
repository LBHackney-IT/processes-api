using System;
using System.Threading.Tasks;
using Hackney.Core.Logging;
using Microsoft.Extensions.Logging;
using Hackney.Core.Http;
using Hackney.Shared.Person.Boundary.Request;
using System.Net.Http;
using Newtonsoft.Json;
using System.Text;
using ProcessesApi.V1.Boundary.Constants;

namespace ProcessesApi.V1.Gateways
{
    public class PersonApiGateway : IPersonApiGateway
    {
        private readonly ILogger<PersonApiGateway> _logger;
        private const string ApiName = "Person";
        private const string PersonApiUrl = "PersonApiUrl";
        private const string PersonApiToken = "PersonApiToken";
        private readonly IApiGateway _apiGateway;

        public PersonApiGateway(ILogger<PersonApiGateway> logger, IApiGateway apiGateway)
        {
            _logger = logger;
            _apiGateway = apiGateway;
            _apiGateway.Initialise(ApiName, PersonApiUrl, PersonApiToken, null);
        }

        [LogCall]
        public async Task UpdatePersonById(Guid id, UpdatePersonRequestObject updatePersonRequestObject, int? ifMatch)
        {
            _logger.LogDebug($"Calling Person API to update person ID: {id}");

            var route = $"{_apiGateway.ApiRoute}/persons/{id}";
            var uri = new Uri(route, UriKind.Relative);
            var message = new HttpRequestMessage(HttpMethod.Patch, uri);

            var requestJson = JsonConvert.SerializeObject(updatePersonRequestObject);
            message.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");

            message.Method = HttpMethod.Patch;
            _apiGateway.RequestHeaders.Add(HeaderConstants.IfMatch, $"\"{ifMatch?.ToString()}\"");

            await _apiGateway.SendAsync(message, Guid.NewGuid()).ConfigureAwait(false);
        }
    }
}
