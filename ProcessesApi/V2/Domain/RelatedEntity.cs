using System;

namespace ProcessesApi.V2.Domain
{
    public class RelatedEntity
    {
        public Guid Id { get; set; }
        public TargetType TargetType { get; set; }

        public SubType SubType { get; set; }
        public string Description { get; set; }

    }
}
