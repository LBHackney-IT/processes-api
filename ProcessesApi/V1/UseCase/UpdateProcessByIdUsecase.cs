using Hackney.Core.JWT;
using Hackney.Core.Logging;
using Hackney.Core.Sns;
using Hackney.Shared.Processes.Boundary.Request;
using Hackney.Shared.Processes.Domain;
using Hackney.Shared.Processes.Factories;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase.Interfaces;
using System;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class UpdateProcessByIdUseCase : IUpdateProcessByIdUseCase
    {
        private readonly IProcessesGateway _processGateway;
        private readonly ISnsGateway _snsGateway;

        public UpdateProcessByIdUseCase(IProcessesGateway processGateway, ISnsGateway snsGateway)

        {
            _processGateway = processGateway;
            _snsGateway = snsGateway;
        }

        [LogCall]
        public async Task<ProcessState> Execute(ProcessQuery query, UpdateProcessByIdRequestObject requestObject, string requestBody, int? ifMatch, Token token)
        {

            var result = await _processGateway.UpdateProcessById(query, requestObject, requestBody, ifMatch).ConfigureAwait(false);

            if (result == null) return null;

            var processSnsMessage = result.CreateProcessUpdatedEvent(query.Id, token);
            var topicArn = Environment.GetEnvironmentVariable("PROCESS_SNS_ARN");
            await _snsGateway.Publish(processSnsMessage, topicArn).ConfigureAwait(false);

            return result.UpdatedEntity;
        }
    }
}
