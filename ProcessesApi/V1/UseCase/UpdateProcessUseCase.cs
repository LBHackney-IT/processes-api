using Hackney.Core.JWT;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Services.Interfaces;
using ProcessesApi.V1.UseCase.Exceptions;
using ProcessesApi.V1.UseCase.Interfaces;
using System;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class UpdateProcessUseCase : IUpdateProcessUseCase
    {
        private readonly IProcessesGateway _processGateway;
        private readonly Func<ProcessName, IProcessService> _processServiceProvider;

        public UpdateProcessUseCase(IProcessesGateway processGateway, Func<ProcessName, IProcessService> processServiceProvider)

        {
            _processGateway = processGateway;
            _processServiceProvider = processServiceProvider;
        }

        public async Task<Process> Execute(UpdateProcessQuery request, UpdateProcessRequestObject requestObject, int? ifMatch, Token token)
        {
            var triggerObject = ProcessTrigger.Create(request.Id, request.ProcessTrigger, requestObject.FormData, requestObject.Documents);

            var process = await _processGateway.GetProcessById(request.Id).ConfigureAwait(false);
            if (process is null) return null;
            if (ifMatch != process.VersionNumber)
                throw new VersionNumberConflictException(ifMatch, process.VersionNumber);

            IProcessService service = _processServiceProvider(request.ProcessName);
            await service.Process(triggerObject, process, token).ConfigureAwait(false);

            await _processGateway.SaveProcess(process).ConfigureAwait(false);

            return process;
        }
    }
}
