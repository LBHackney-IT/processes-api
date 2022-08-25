namespace ProcessesApi.V2.Infrastructure
{
    public static class PersonEventConstants
    {
        // JWT TOKEN
        public const string V1_VERSION = "V1";
        public const string SOURCE_DOMAIN = "Person";
        public const string SOURCE_SYSTEM = "ProcessesAPI";

        public const string PERSON_CREATED_EVENT = "PersonCreatedEvent";
        public const string PERSON_UPDATED_EVENT = "PersonUpdatedEvent";
    }
}
