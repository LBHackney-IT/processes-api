using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Gateways;
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
        private readonly Func<string, IProcessService> _processDelegate;

        public ProcessUseCase(IProcessesGateway processGateway, Func<string, IProcessService> processDelegate)
        {
            _processGateway = processGateway;
            _processDelegate = processDelegate;
        }

        public async Task<Process> Execute(Guid id, string processTrigger, Guid? targetId, List<Guid> relatedEntities, Dictionary<string, object> formData, List<Guid> documents, string processName, int? ifMatch)
        {
            var triggerObject = UpdateProcessState.Create(id,
                                                          targetId,
                                                          processTrigger,
                                                          formData,
                                                          documents,
                                                          relatedEntities);

            Process process;

            if (processTrigger != ProcessInternalTriggers.StartApplication)
            {
                process = await _processGateway.GetProcessById(id).ConfigureAwait(false);
                if (process is null) return null;
                if (ifMatch != process.VersionNumber)
                    throw new VersionNumberConflictException(ifMatch, process.VersionNumber);
            }
            else
            {
                process = Process.Create(id, new List<ProcessState>(), null, targetId.Value, relatedEntities, processName, null);
            }

            var processService = _processDelegate(processName);
            await processService.Process(triggerObject, process).ConfigureAwait(false);

            await _processGateway.SaveProcess(process).ConfigureAwait(false);

            return process;
        }
    }
}
