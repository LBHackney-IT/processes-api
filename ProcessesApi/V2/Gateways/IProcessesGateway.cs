using Hackney.Core.DynamoDb;
using ProcessesApi.V2.Boundary.Request;
using ProcessesApi.V2.Domain;
using ProcessesApi.V2.Infrastructure;
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
