using Hackney.Core.JWT;
using Hackney.Core.Sns;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase.Exceptions;
using ProcessesApi.V1.UseCase.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ProcessesApi.V1.Infrastructure;

namespace ProcessesApi.V1.UseCase
{
    public class SoleToJointUseCase : ISoleToJointUseCase
    {
        private readonly IProcessesGateway _processGateway;
        private readonly ISoleToJointService _soleToJointService;
        private readonly ISnsGateway _snsGateway;
        private readonly ISnsFactory _snsFactory;

        public SoleToJointUseCase(IProcessesGateway processGateway, ISoleToJointService soleToJointService,
                                  ISnsGateway snsGateway, ISnsFactory snsFactory)

        {
            _processGateway = processGateway;
            _soleToJointService = soleToJointService;
            _snsGateway = snsGateway;
            _snsFactory = snsFactory;
        }

        public async Task<Process> Execute(Guid id, string processTrigger, Guid? targetId, List<Guid> relatedEntities, Dictionary<string, object> formData, List<Guid> documents, string processName, int? ifMatch, Token token)
        {
            var triggerObject = UpdateProcessState.Create(id,
                                                          targetId,
                                                          processTrigger,
                                                          formData,
                                                          documents,
                                                          relatedEntities);

            Process process;

            if (processTrigger != SoleToJointInternalTriggers.StartApplication)
            {
                process = await _processGateway.GetProcessById(id).ConfigureAwait(false);
                if (process is null) return null;
                if (ifMatch != process.VersionNumber)
                    throw new VersionNumberConflictException(ifMatch, process.VersionNumber);
            }
            else
            {
                process = Process.Create(id, new List<ProcessState>(), null, targetId.Value, relatedEntities, processName, null);
                var processSnsMessage = _snsFactory.Create(process, token, ProcessEventConstants.PROCESS_STARTED_EVENT, process);
                var processTopicArn = Environment.GetEnvironmentVariable("PROCESS_SNS_ARN");

                await _snsGateway.Publish(processSnsMessage, processTopicArn).ConfigureAwait(false);
            }

            await _soleToJointService.Process(triggerObject, process, token).ConfigureAwait(false);

            await _processGateway.SaveProcess(process).ConfigureAwait(false);

            return process;
        }
    }
}
