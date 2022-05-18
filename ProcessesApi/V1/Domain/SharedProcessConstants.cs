namespace ProcessesApi.V1.Domain
{
    public static class SharedProcessStates
    {
        public const string ApplicationInitialised = "ApplicationInitialised";
        public const string ProcessClosed = "ProcessClosed";
        public const string ProcessCancelled = "ProcessCancelled";
    }

    public static class SharedInternalTriggers
    {
        public const string StartApplication = "StartApplication";
    }
}
