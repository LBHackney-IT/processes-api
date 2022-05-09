using System;

namespace ProcessesApi.V1.Services.Exceptions
{
    public class InvalidTriggerException : Exception
    {
        public InvalidTriggerException() : base("Invalid trigger.")
        {
        }

        public InvalidTriggerException(string trigger, string state) : base($"Cannot trigger {trigger} from {state}.")
        {
        }
    }
}
