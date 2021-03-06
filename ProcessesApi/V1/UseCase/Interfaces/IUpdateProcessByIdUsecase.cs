using Hackney.Core.JWT;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface IUpdateProcessByIdUseCase
    {
        Task<ProcessState> Execute(ProcessQuery query, UpdateProcessByIdRequestObject requestObject, string requestBody, int? ifMatch, Token token);
    }
}

