using Hackney.Core.JWT;
using Hackney.Core.Sns;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class UpdateProcessByIdUsecase : IUpdateProcessByIdUsecase
    {
        private readonly IProcessesGateway _processGateway;
        private readonly ISnsGateway _snsGateway;
        private readonly ISnsFactory _snsFactory;

        public UpdateProcessByIdUsecase(IProcessesGateway processGateway, ISnsGateway snsGateway, ISnsFactory snsFactory)

        {
            _processGateway = processGateway;
            _snsFactory = snsFactory;
            _snsGateway = snsGateway;
        }

        public async Task<ProcessState> Execute(ProcessQuery query, UpdateProcessByIdRequestObject requestObject, string requestBody, int? ifMatch, Token token)
        {

            var result = await _processGateway.UpdateProcessById(query, requestObject, requestBody, ifMatch).ConfigureAwait(false);

            if (result == null) return null;
            var processSnsMessage = _snsFactory.ProcessUpdated(query.Id, result, token);
            var topicArn = Environment.GetEnvironmentVariable("PROCESS_SNS_ARN");
            await _snsGateway.Publish(processSnsMessage, topicArn).ConfigureAwait(false);


            return result.UpdatedEntity;
        }
    }
}
