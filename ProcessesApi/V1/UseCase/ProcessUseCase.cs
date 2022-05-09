using Hackney.Core.JWT;
using Hackney.Core.Sns;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Services.Interfaces;
using ProcessesApi.V1.UseCase.Exceptions;
using ProcessesApi.V1.UseCase.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class ProcessUseCase : IProcessUseCase
    {
        private readonly IProcessesGateway _processGateway;
        private readonly Func<ProcessName, IProcessService> _processServiceProvider;

        public ProcessUseCase(IProcessesGateway processGateway, Func<ProcessName, IProcessService> processServiceProvider)

        {
            _processGateway = processGateway;
            _processServiceProvider = processServiceProvider;
        }

        public async Task<Process> Execute(Guid id, string processTrigger, Guid? targetId, List<Guid> relatedEntities, Dictionary<string, object> formData, List<Guid> documents, ProcessName processName, int? ifMatch, Token token)
        {
            var triggerObject = UpdateProcessState.Create(id,
                                                          targetId,
                                                          processTrigger,
                                                          formData,
                                                          documents,
                                                          relatedEntities);

            Process process;

            if (processTrigger == SharedInternalTriggers.StartApplication)
            {
                process = Process.Create(id, new List<ProcessState>(), null, targetId.Value, relatedEntities, processName, null);
            }
            else
            {
                process = await _processGateway.GetProcessById(id).ConfigureAwait(false);
                if (process is null) return null;
                if (ifMatch != process.VersionNumber)
                    throw new VersionNumberConflictException(ifMatch, process.VersionNumber);
            }

            IProcessService service = _processServiceProvider(processName);
            await service.Process(triggerObject, process, token).ConfigureAwait(false);

            await _processGateway.SaveProcess(process).ConfigureAwait(false);

            return process;
        }
    }
}
