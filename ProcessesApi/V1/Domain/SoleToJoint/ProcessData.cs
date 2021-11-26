using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Domain.SoleToJoint
{
    public class ProcessData
    {
        public Guid TargetId { get; }

        public string FormData { get; }
        public IList<Guid> Documents { get; }
        private ProcessData(Guid targetId, string formData, IList<Guid> documents)
        {
            TargetId = targetId;
            FormData = formData;
            Documents = documents;
        }

        public static ProcessData Create(Guid targetId, string formData, IList<Guid> documents)
        {
            return new ProcessData(targetId, formData, documents);
        }
    }
}
