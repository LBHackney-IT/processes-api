using ProcessesApi.V1.Domain;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface IProcessService
    {
        Task Process(UpdateProcessState processRequest, Process process);
    }
}
