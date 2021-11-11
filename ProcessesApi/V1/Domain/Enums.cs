using System.Text.Json.Serialization;

namespace ProcessesApi.V1.Domain
{

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum Title
    {
        Dr,
        Master,
        Miss,
        Mr,
        Mrs,
        Ms,
        Other,
        Rabbi,
        Reverand
    }
}
