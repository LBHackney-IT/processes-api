using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain.SoleToJoint;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface ISoleToJointUseCase
    {
        Task<SoleToJointProcess> Execute(SoleToJointRequest request);
    }
}
