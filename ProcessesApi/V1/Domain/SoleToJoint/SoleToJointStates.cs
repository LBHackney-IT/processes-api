namespace ProcessesApi.V1.Domain.SoleToJoint
{
    public static class SoleToJointStates
    {
        public const string SelectTenants = "SelectTenants";
        public const string AutomatedChecksFailed = "AutomatedChecksFailed";
        public const string AutomatedChecksPassed = "AutomatedChecksPassed";
        public const string ManualChecksFailed = "ManualChecksFailed";
        public const string ManualChecksPassed = "ManualChecksPassed";
        public const string BreachChecksFailed = "BreachChecksFailed";
        public const string BreachChecksPassed = "BreachChecksPassed";
        public const string DocumentsRequestedDes = "DocumentsRequestedDes";
        public const string DocumentsRequestedAppointment = "DocumentsRequestedAppointment";
        public const string DocumentsAppointmentRescheduled = "DocumentsAppointmentRescheduled";
        public const string DocumentChecksPassed = "DocumentChecksPassed";
        public const string ApplicationSubmitted = "ApplicationSubmitted";
        public const string TenureInvestigationFailed = "TenureInvestigationFailed";
        public const string TenureInvestigationPassed = "TenureInvestigationPassed";
        public const string TenureInvestigationPassedWithInt = "TenureInvestigationPassedWithInt";
        public const string InterviewScheduled = "InterviewScheduled";
    }
}
