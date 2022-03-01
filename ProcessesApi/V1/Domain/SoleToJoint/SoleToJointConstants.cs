namespace ProcessesApi.V1.Domain
{
    public static class SoleToJointStates
    {
        public const string ApplicationInitialised = "ApplicationInitialised";
        public const string SelectTenants = "SelectTenants";
        public const string AutomatedChecksFailed = "AutomatedChecksFailed";
        public const string AutomatedChecksPassed = "AutomatedChecksPassed";
        public const string ProcessCancelled = "ProcessCancelled";
        public const string ManualChecksFailed = "ManualChecksFailed";
        public const string ManualChecksPassed = "ManualChecksPassed";
        public const string ConfirmAppointmentScheduled = "ConfirmAppointmentScheduled";
    }

    public static class SoleToJointPermittedTriggers
    {
        public const string CheckEligibility = "CheckEligibility";
        public const string CancelProcess = "CancelProcess";
        public const string CheckManualEligibility = "CheckManualEligibility";
        public const string RequestDocuments = "RequestDocuments";
        public const string CheckTenancyBreach = "CheckTenancyBreach";
    }

    public static class SoleToJointInternalTriggers
    {
        public const string StartApplication = "StartApplication";
        public const string EligibiltyPassed = "EligibiltyPassed";
        public const string EligibiltyFailed = "EligibiltyFailed";
        public const string ManualEligibilityFailed = "ManualEligibilityFailed";
        public const string ManualEligibilityPassed = "ManualEligibilityPassed";
    }

    // NOTE: Form data key values must not be capitalised or E2E tests will fail
    public static class SoleToJointFormDataKeys
    {
        public const string IncomingTenantId = "incomingTenantId";
        public const string BR11 = "br11";
        public const string BR12 = "br12";
        public const string BR13 = "br13";
        public const string BR15 = "br15";
        public const string BR16 = "br16";
    }

}
