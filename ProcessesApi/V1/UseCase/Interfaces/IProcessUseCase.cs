using ProcessesApi.V1.Domain;
using System.Threading.Tasks;
using Hackney.Core.JWT;
using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface IProcessUseCase
    {
        Task<Process> Execute(Guid id,
                              string processTrigger,
                              Guid? targetId,
                              List<Guid> relatedEntities,
                              Dictionary<string, object> formData,
                              List<Guid> documents,
                              ProcessName processName,
                              int? ifMatch,
                              Token token);
    }
}
