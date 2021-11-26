using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Domain.Enums
{
    public enum SoleToJointStates
    {
        SelectTenants,
        CheckingEligibility,
        AutomatedChecksFailed,
        AutomatedChecksPassed,
        ManualChecksFailed,
        ManualChecksPassed,
        ConfirmAppointmentScheduled
    }

    public enum SoleToJoinPermittedTriggers
    {
        CheckEligibility,
        CheckManualEligibility,
        ExitApplication,
        RequestDocuments,
        CheckTenancyBreach
    }

    public enum SoleToJointTriggers
    {
        StartApplication,
        CheckEligibility,
        CheckManualEligibility,
        RequestDocuments,
        CheckTenancyBreach
    }
}
