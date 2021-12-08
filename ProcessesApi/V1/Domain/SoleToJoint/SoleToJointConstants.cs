namespace ProcessesApi.V1.Domain
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

}
