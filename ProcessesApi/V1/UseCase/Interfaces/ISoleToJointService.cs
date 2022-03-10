using ProcessesApi.V1.Domain;
using System.Threading.Tasks;
using Hackney.Core.JWT;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface ISoleToJointService
    {
        Task Process(UpdateProcessState processRequest, Process soleToJointProcess, Token token);
    }
}
