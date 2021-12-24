namespace ProcessesApi.V1.Domain
{
    public static class ProcessStates
    {
        public const string ApplicationInitialised = "ApplicationInitialised";
        public const string ProcessClosed = "ProcessClosed";
    }

    public static class ProcessInternalTriggers
    {
        public const string StartApplication = "StartApplication";
        public const string CancelProcess = "CancelProcess";
        public const string ExitApplication = "ExitApplication";
    }

}
