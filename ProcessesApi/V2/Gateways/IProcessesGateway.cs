using ProcessesApi.V2.Domain;
using System;
using System.Threading.Tasks;

namespace ProcessesApi.V2.Gateways
{
    public interface IProcessesGateway
    {
        Task<Process> GetProcessById(Guid id);
        Task<Process> SaveProcess(Process query);
    }
}
