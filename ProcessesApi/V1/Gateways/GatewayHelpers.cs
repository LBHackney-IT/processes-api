using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProcessesApi.V1.Gateways
{
    public static class GatewayHelpers
    {
        public static JsonSerializerOptions GetJsonSerializerOptions()
        {
            var options = new JsonSerializerOptions
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                WriteIndented = true,
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
            options.Converters.Add(new JsonStringEnumConverter());

            return options;
        }
    }
}
