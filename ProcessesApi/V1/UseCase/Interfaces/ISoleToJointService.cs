using ProcessesApi.V1.Domain.Enums;
using ProcessesApi.V1.Domain.SoleToJoint;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface ISoleToJointService
    {
        Task Process(SoleToJointObject<SoleToJointTriggers> processRequest, SoleToJointProcess soleToJointProcess);
    }
}
