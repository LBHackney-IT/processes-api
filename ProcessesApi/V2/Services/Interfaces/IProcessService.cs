using System.Threading.Tasks;
using Hackney.Core.JWT;
using ProcessesApi.V2.Domain;

namespace ProcessesApi.V2.Services.Interfaces
{
    public interface IProcessService
    {
        public Task Process(ProcessTrigger processRequest, Process soleToJointProcess, Token token);
    }
}
