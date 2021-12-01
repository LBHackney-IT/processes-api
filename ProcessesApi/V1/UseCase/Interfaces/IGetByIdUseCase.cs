using System.Threading.Tasks;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain.SoleToJoint;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface IGetByIdUseCase
    {
        Task<SoleToJointProcess> Execute(ProcessesQuery query);
    }
}
