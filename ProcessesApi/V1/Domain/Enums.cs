using System.Text.Json.Serialization;

namespace ProcessesApi.V1.Domain
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ProcessName
    {
        soletojoint
    }
}
