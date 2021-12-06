using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain.Enums;
using ProcessesApi.V1.Domain.SoleToJoint;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface ISoleToJointUseCase
    {
        Task<SoleToJointProcess> Execute(Guid id, string processTrigger, Guid? targetId, List<Guid> relatedEntities, object formData, List<Guid> documents, string processName);
    }
}
