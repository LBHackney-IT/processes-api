using Hackney.Core.DynamoDb;
using Hackney.Shared.Processes.Boundary.Request;
using Hackney.Shared.Processes.Domain;
using Hackney.Shared.Processes.Infrastructure;
using System;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Gateways
{
    public interface IProcessesGateway
    {
        Task<Process> GetProcessById(Guid id);
        Task<Process> SaveProcess(Process query);
        Task<UpdateEntityResult<ProcessState>> UpdateProcessById(ProcessQuery query, UpdateProcessByIdRequestObject requestObject, string requestBody, int? ifMatch);
        Task<PagedResult<Process>> GetProcessesByTargetId(GetProcessesByTargetIdRequest request);
    }
}
