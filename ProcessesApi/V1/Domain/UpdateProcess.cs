using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Domain
{
    public class UpdateProcess
    {
        private UpdateProcess(Guid id, ProcessData processData, Assignment assignment)
        {
            Id = id;
            FormData = processData.FormData;
            Documents = processData.Documents;
            Assignment = assignment;
        }

        public Guid Id { get; private set; }
        public Guid? TargetId { get; private set; }
        public Dictionary<string, Object> FormData { get; private set; }
        public List<Guid> Documents { get; private set; }
        public Assignment Assignment { get; private set; }

        public static UpdateProcess Create(Guid id, ProcessData processData, Assignment assignment)
        {
            return new UpdateProcess(id, processData, assignment);
        }
    }
}
