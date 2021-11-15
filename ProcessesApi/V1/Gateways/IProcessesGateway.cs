using ProcessesApi.V1.Domain;
using System;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Gateways
{
    public interface IProcessesGateway
    {
        Task<Process> GetProcessById(Guid id);

    }
}
