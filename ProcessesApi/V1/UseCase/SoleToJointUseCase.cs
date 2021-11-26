using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.Enums;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class SoleToJointUseCase : ISoleToJointUseCase
    {
        private readonly IProcessesGateway _processGateway;
        private readonly ISoleToJointService _soleToJointService;

        public SoleToJointUseCase(IProcessesGateway processGateway, ISoleToJointService soleToJointService)
        {
            _processGateway = processGateway;
            _soleToJointService = soleToJointService;
        }

        public async Task<SoleToJointProcess> Execute(SoleToJointRequest request)
        {
            var processTrigger = SoleToJointObject<SoleToJointTriggers>.Create(
                request.Id,
                Enum.Parse<SoleToJointTriggers>(request.Trigger),
                ProcessData.Create(request.ProcessRequest.TargetId, request.ProcessRequest.FormData.ToString(), request.ProcessRequest.Documents));

            SoleToJointProcess process;

            if (processTrigger.Trigger != SoleToJointTriggers.StartApplication)
            {
                process = await _processGateway.GetProcess(request.Id);
            }
            else
            {
                process = SoleToJointProcess.Create(Guid.NewGuid(), new List<ProcessState<SoleToJointStates, SoleToJointTriggers>>(), null);
            }

            await _soleToJointService.Process(processTrigger, process);

            await _processGateway.Save(process);

            return process;
        }
    }
}
