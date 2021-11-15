using System;
using System.Threading.Tasks;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Boundary.Request;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface IGetByIdUseCase
    {
        Task<Process> Execute(ProcessesQuery query);
    }
}
