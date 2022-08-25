using System;

namespace ProcessesApi.V2.Domain
{
    public class PatchAssignmentEntity
    {
        public Guid PatchId { get; set; }
        public string PatchName { get; set; }
        public Guid ResponsibleEntityId { get; set; }
        public string ResponsibleName { get; set; }
    }
}
