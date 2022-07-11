using Hackney.Core.JWT;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Constants;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Services.Interfaces;
using ProcessesApi.V1.UseCase.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
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

        public async Task<Process> Execute(CreateProcess request, ProcessName processName, Token token)
        {
            var process = Process.Create(Guid.NewGuid(), new List<ProcessState>(), null, request.TargetId, request.TargetType, request.RelatedEntities, processName, null);
            var triggerObject = ProcessTrigger.Create(process.Id, SharedPermittedTriggers.StartApplication, request.FormData, request.Documents);

            IProcessService service = _processServiceProvider(processName);
            await service.Process(triggerObject, process, token).ConfigureAwait(false);

            await _processGateway.SaveProcess(process).ConfigureAwait(false);

            return process;
        }
    }
}
