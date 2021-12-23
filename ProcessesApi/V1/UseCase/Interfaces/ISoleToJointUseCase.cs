using ProcessesApi.V1.Domain;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface ISoleToJointUseCase
    {
        Task<Process> Execute(Guid id,
                              string processTrigger,
                              Guid? targetId,
                              List<Guid> relatedEntities,
                              Dictionary<string, object> formData,
                              List<Guid> documents,
                              string processName,
                              int? ifMatch);
    }
}
