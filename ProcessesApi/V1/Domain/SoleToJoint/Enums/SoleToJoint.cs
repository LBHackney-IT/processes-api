using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Domain.Enums
{
    public static class SoleToJointStates
    {
        public const string ApplicationInitialised = "ApplicationInitialised";
        public const string SelectTenants = "SelectTenants";
        public const string AutomatedChecksFailed = "AutomatedChecksFailed";
        public const string AutomatedChecksPassed = "AutomatedChecksPassed";
        public const string ManualChecksFailed = "ManualChecksFailed";
        public const string ManualChecksPassed = "ManualChecksPassed";
        public const string ConfirmAppointmentScheduled = "ConfirmAppointmentScheduled";

        //public bool IsValid(string val)
        //{
        //    return (val == ApplicationInitialised)
        //        || (val == SelectTenants);
        //}
    }

    public static class SoleToJointPermittedTriggers
    {
        public const string CheckEligibility = "CheckEligibility";
        public const string CheckManualEligibility = "CheckManualEligibility";
        public const string ExitApplication = "ExitApplication";
        public const string RequestDocuments = "RequestDocuments";
        public const string CheckTenancyBreach = "CheckTenancyBreach";
    }

    public static class SoleToJointTriggers
    {
        public const string StartApplication = "StartApplication";
        public const string CheckEligibility = "CheckEligibility";
        public const string CheckManualEligibility = "CheckManualEligibility";
        public const string RequestDocuments = "RequestDocuments";
        public const string CheckTenancyBreach = "CheckTenancyBreach";
    }

    //[JsonConverter(typeof(JsonStringEnumConverter))]
    //public enum SoleToJointStates
    //{
    //    ApplicationInitialised,
    //    SelectTenants,
    //    AutomatedChecksFailed,
    //    AutomatedChecksPassed,
    //    ManualChecksFailed,
    //    ManualChecksPassed,
    //    ConfirmAppointmentScheduled
    //}

    //public enum SoleToJointPermittedTriggers
    //{
    //    CheckEligibility,
    //    CheckManualEligibility,
    //    ExitApplication,
    //    RequestDocuments,
    //    CheckTenancyBreach
    //}

    //[JsonConverter(typeof(JsonStringEnumConverter))]
    //public enum SoleToJointTriggers
    //{
    //    StartApplication,
    //    CheckEligibility,
    //    CheckManualEligibility,
    //    RequestDocuments,
    //    CheckTenancyBreach
    //}
}
