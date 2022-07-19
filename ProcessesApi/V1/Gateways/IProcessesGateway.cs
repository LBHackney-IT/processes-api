using Hackney.Core.DynamoDb;
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
        Task<UpdateEntityResult<ProcessState>> UpdateProcessById(ProcessQuery query, UpdateProcessByIdRequestObject requestObject, string requestBody, int? ifMatch);
        Task<PagedResult<Process>> GetProcessByTargetId(GetProcessByTargetIdRequest request);
    }
}
