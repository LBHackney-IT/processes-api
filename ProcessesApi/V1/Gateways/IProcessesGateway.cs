using ProcessesApi.V1.Domain;
using System;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Gateways
{
    public interface IProcessesGateway
    {
        Task<Process> GetProcessById(Guid id);
        Task<Process> SaveProcess(Process query);
        Task<Process> SaveProcessById(UpdateProcess query, int? ifMatch);
    }
}
