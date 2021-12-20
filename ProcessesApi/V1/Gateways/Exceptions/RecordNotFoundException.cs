using System;

namespace ProcessesApi.V1.Gateways.Exceptions
{
    public class RecordNotFoundException : Exception
    {
        public Type TargetType { get; private set; }
        public Guid TargetId { get; private set; }

        public RecordNotFoundException(Type targetType, Guid targetId)
            : base(string.Format("The ID supplied ({0}) does not exist for entity type {1}.",
                                 targetId.ToString(),
                                 targetType.ToString()))
        {
            TargetType = targetType;
            TargetId = targetId;
        }
    }
}
