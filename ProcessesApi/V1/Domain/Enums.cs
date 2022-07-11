using System.Text.Json.Serialization;

namespace ProcessesApi.V1.Domain
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum ProcessName
    {
        soletojoint,
        changeofname
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum TargetType
    {
        tenure,
        person,
        asset
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SubType
    {
        tenant,
        householdMember,
        newTenure
    }
}
