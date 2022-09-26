using System.Threading.Tasks;
using Hackney.Core.JWT;
using Hackney.Shared.Processes.Domain;

namespace ProcessesApi.V1.Services.Interfaces
{
    public interface IProcessService
    {
        public Task Process(ProcessTrigger processRequest, Process soleToJointProcess, Token token);
    }
}
