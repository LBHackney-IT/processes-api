using Amazon.DynamoDBv2.DataModel;
using Hackney.Core.Logging;
using Microsoft.Extensions.Logging;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure;
using ProcessesApi.V1.UseCase.Exceptions;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Gateways
{
    public class ProcessesGateway : IProcessesGateway
    {
        private readonly IDynamoDBContext _dynamoDbContext;
        private readonly ILogger<ProcessesGateway> _logger;



        public ProcessesGateway(IDynamoDBContext dynamoDbContext, ILogger<ProcessesGateway> logger)
        {
            _dynamoDbContext = dynamoDbContext;
            _logger = logger;
        }

        [LogCall]
        public async Task<Process> GetProcessById(Guid id)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for ID: {id}");

            var result = await _dynamoDbContext.LoadAsync<ProcessesDb>(id).ConfigureAwait(false);
            return result?.ToDomain();
        }

        [LogCall]
        public async Task<Process> SaveProcess(Process query)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.SaveAsync for id {query.Id}");
            var processDbEntity = query.ToDatabase();

            await _dynamoDbContext.SaveAsync(processDbEntity).ConfigureAwait(false);
            return processDbEntity.ToDomain();
        }

        [LogCall]
        public async Task<UpdateProcessGatewayResult> UpdateProcessById(ProcessQuery query, UpdateProcessByIdRequestObject requestObject, int? ifMatch)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for ID: {query.Id}");

            var currentProcess = await _dynamoDbContext.LoadAsync<ProcessesDb>(query.Id).ConfigureAwait(false);
            if (currentProcess == null) return null;


            if (ifMatch != currentProcess.VersionNumber)
                throw new VersionNumberConflictException(ifMatch, currentProcess.VersionNumber);

            var processData = ProcessData.Create(requestObject.FormData, requestObject.Documents);
            var UpdatedcurrentState = ProcessState.Create(currentProcess.CurrentState.State,
                                                          currentProcess.CurrentState.PermittedTriggers,
                                                          requestObject.Assignment ?? currentProcess.CurrentState.Assignment,
                                                          processData,
                                                          currentProcess.CurrentState.CreatedAt,
                                                          DateTime.UtcNow);


            var updateProcess = Process.Create(query.Id,
                                               currentProcess.PreviousStates,
                                               UpdatedcurrentState,
                                               currentProcess.TargetId,
                                               currentProcess.RelatedEntities,
                                               currentProcess.ProcessName,
                                               ifMatch);

            var dbEntity = updateProcess.ToDatabase();
            _logger.LogDebug($"Calling IDynamoDBContext.SaveAsync for id {query.Id}");

            await _dynamoDbContext.SaveAsync(dbEntity).ConfigureAwait(false);
            return new UpdateProcessGatewayResult(currentProcess.ToDomain(), dbEntity.ToDomain());
        }


    }
}
