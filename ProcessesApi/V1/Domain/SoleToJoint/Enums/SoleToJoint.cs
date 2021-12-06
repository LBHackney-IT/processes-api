using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Domain.Enums
{
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SoleToJointStates
    {
        ApplicationInitialised,
        SelectTenants,
        AutomatedChecksFailed,
        AutomatedChecksPassed,
        ManualChecksFailed,
        ManualChecksPassed,
        ConfirmAppointmentScheduled
    }

    public enum SoleToJointPermittedTriggers
    {
        CheckEligibility,
        CheckManualEligibility,
        ExitApplication,
        RequestDocuments,
        CheckTenancyBreach
    }

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public enum SoleToJointTriggers
    {
        StartApplication,
        CheckEligibility,
        CheckManualEligibility,
        RequestDocuments,
        CheckTenancyBreach
    }
}
