using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Boundary.Request;
using System;
using System.Threading.Tasks;
using ProcessesApi.V1.Domain.SoleToJoint;

namespace ProcessesApi.V1.Gateways
{
    public interface IProcessesGateway
    {
        Task<SoleToJointProcess> GetProcessById(Guid id);
        Task<SoleToJointProcess> SaveProcess(SoleToJointProcess query);
    }
}
