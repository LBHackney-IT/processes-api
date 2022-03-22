using Hackney.Core.JWT;
using Hackney.Core.Sns;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class UpdateProcessUsecase : IUpdateProcessByIdUsecase
    {
        private readonly IProcessesGateway _processGateway;
        private readonly ISnsGateway _snsGateway;
        private readonly ISnsFactory _snsFactory;

        public UpdateProcessUsecase(IProcessesGateway processGateway,
                                  ISnsGateway snsGateway, ISnsFactory snsFactory)

        {
            _processGateway = processGateway;
            _snsGateway = snsGateway;
            _snsFactory = snsFactory;
        }

        public async Task<Process> Execute(Guid id, Dictionary<string, object> formData, List<Guid> documents, Assignment assignment, string processName, int? ifMatch, Token token)
        {
            var processData = ProcessData.Create(formData, documents);
            var updateProcess = UpdateProcess.Create(id, processData, assignment);

            var response = await _processGateway.SaveProcessById(updateProcess, ifMatch).ConfigureAwait(false);

            return response;
        }
    }
}
