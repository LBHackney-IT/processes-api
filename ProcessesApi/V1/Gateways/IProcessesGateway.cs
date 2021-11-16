using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Boundary.Request;
using System;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Gateways
{
    public interface IProcessesGateway
    {
        Task<Process> GetProcessById(Guid id);
        Task<Process> CreateNewProcess(CreateProcessQuery createProcessQuery);
    }
}
