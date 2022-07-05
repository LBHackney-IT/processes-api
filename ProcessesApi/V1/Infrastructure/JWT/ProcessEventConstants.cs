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

        public const string PROCESS_STARTED_AGAINST_TENURE_EVENT = "ProcessStartedAgainstTenureEvent";
        public const string PROCESS_STARTED_AGAINST_ASSET_EVENT = "ProcessStartedAgainstAssetEvent";
        public const string PROCESS_STARTED_AGAINST_PERSON_EVENT = "ProcessStartedAgainstPersonEvent";

    }
}
