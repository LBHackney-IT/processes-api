using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Infrastructure;
using System;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Gateways
{
    public interface IProcessesGateway
    {
        Task<Process> GetProcessById(Guid id);
        Task<Process> SaveProcess(Process query);
        Task<Process> SaveProcessById(UpdateProcessByIdQuery query, UpdateProcessByIdRequestObject requestObject, int? ifMatch);
    }
}
