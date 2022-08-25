using Hackney.Core.JWT;
using Hackney.Core.Logging;
using ProcessesApi.V2.Boundary.Request;
using ProcessesApi.V1.Constants;
using ProcessesApi.V2.Domain;
using ProcessesApi.V2.Gateways;
using ProcessesApi.V2.Services.Interfaces;
using ProcessesApi.V2.UseCase.Interfaces;
using System;
using System.Threading.Tasks;

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
            var process = Process.Create(request.TargetId, request.TargetType, request.RelatedEntities, processName, request.PatchAssignmentEntity);

            var triggerObject = ProcessTrigger.Create(process.Id, SharedPermittedTriggers.StartApplication, request.FormData, request.Documents);

            IProcessService service = _processServiceProvider(processName);
            await service.Process(triggerObject, process, token).ConfigureAwait(false);

            await _processGateway.SaveProcess(process).ConfigureAwait(false);

            return process;
        }
    }
}
