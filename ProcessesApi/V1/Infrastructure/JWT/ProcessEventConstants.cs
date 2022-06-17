namespace ProcessesApi.V1.Infrastructure.JWT
{
    public class ProcessEventConstants
    {
        public const string V1_VERSION = "v1";
        public const string SOURCE_DOMAIN = "Processes";
        public const string SOURCE_SYSTEM = "ProcessesAPI";

        public const string PROCESS_STARTED_EVENT = "ProcessStartedEvent";
        public const string PROCESS_UPDATED_EVENT = "ProcessUpdatedEvent";
        public const string PROCESS_CLOSED_EVENT = "ProcessClosedEvent";
        public const string PROCESS_COMPLETED_EVENT = "ProcessCompletedEvent";

    }
}
