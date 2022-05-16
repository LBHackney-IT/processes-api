using System.Threading.Tasks;
using Hackney.Core.JWT;
using ProcessesApi.V1.Domain;

namespace ProcessesApi.V1.Services.Interfaces
{
    public interface IProcessService
    {
        public Task Process(UpdateProcessState processRequest, Process soleToJointProcess, Token token);
    }
}
