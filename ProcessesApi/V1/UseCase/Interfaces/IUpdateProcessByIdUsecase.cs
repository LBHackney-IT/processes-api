using Hackney.Core.JWT;
using Hackney.Shared.Processes.Boundary.Request;
using Hackney.Shared.Processes.Domain;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface IUpdateProcessByIdUseCase
    {
        Task<ProcessState> Execute(ProcessQuery query, UpdateProcessByIdRequestObject requestObject, string requestBody, int? ifMatch, Token token);
    }
}

