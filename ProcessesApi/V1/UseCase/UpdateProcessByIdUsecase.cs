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

        public UpdateProcessByIdUsecase(IProcessesGateway processGateway)

        {
            _processGateway = processGateway;
        }

        public async Task<Process> Execute(UpdateProcessByIdQuery query, UpdateProcessByIdRequestObject requestObject, int? ifMatch)
        {

            var response = await _processGateway.SaveProcessById(query, requestObject, ifMatch).ConfigureAwait(false);

            return response;
        }
    }
}
