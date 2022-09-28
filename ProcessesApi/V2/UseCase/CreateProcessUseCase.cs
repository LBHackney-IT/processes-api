using Hackney.Core.JWT;
using Hackney.Core.Logging;
using Hackney.Shared.Processes.Domain.Constants;
using Hackney.Shared.Processes.Domain;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Services.Interfaces;
using ProcessesApi.V2.UseCase.Interfaces;
using System;
using System.Threading.Tasks;
using Hackney.Shared.Processes.Boundary.Request.V2;

namespace ProcessesApi.V2.UseCase
{
    public class CreateProcessUseCase : ICreateProcessUseCase
    {
        private readonly IProcessesGateway _processGateway;
        private readonly Func<ProcessName, IProcessService> _processServiceProvider;

        public CreateProcessUseCase(IProcessesGateway processGateway, Func<ProcessName, IProcessService> processServiceProvider)

        {
            _processGateway = processGateway;
            _processServiceProvider = processServiceProvider;
        }

        [LogCall]
        public async Task<Process> Execute(CreateProcess request, ProcessName processName, Token token)
        {
            var process = Process.Create(request.TargetId, request.TargetType, request.RelatedEntities, processName, request.PatchAssignment);

            var triggerObject = ProcessTrigger.Create(process.Id, SharedPermittedTriggers.StartApplication, request.FormData, request.Documents);

            IProcessService service = _processServiceProvider(processName);
            await service.Process(triggerObject, process, token).ConfigureAwait(false);

            await _processGateway.SaveProcess(process).ConfigureAwait(false);

            return process;
        }
    }
}
