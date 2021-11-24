using Amazon.DynamoDBv2.DataModel;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure;
using Hackney.Core.Logging;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Infrastructure.Exceptions;

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
        public async Task<Process> CreateNewProcess(CreateProcessQuery query, string processName)
        {
            _logger.LogDebug("Calling IDynamoDBContext.SaveAsync");
            var processDbEntity = query.ToDatabase();
            processDbEntity.ProcessName = processName;

            await _dynamoDbContext.SaveAsync(processDbEntity).ConfigureAwait(false);
            return processDbEntity.ToDomain();
        }

        private ProcessesDb CreateUpdatedProcess(ProcessesDb process, UpdateProcessQueryObject requestObject, UpdateProcessQuery query)
        {
            process.PreviousStates.Add(process.CurrentState);
            process.CurrentState = new ProcessState
            {
                StateName = query.ProcessTrigger,
                ProcessData = new ProcessData
                {
                    FormData = requestObject.FormData,
                    Documents = requestObject.Documents
                },
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
            // TODO: Update to add Assignment and PermittedTriggers when stateless is implemented
            return process;
        }

        [LogCall]
        public async Task<Process> UpdateProcess(UpdateProcessQueryObject requestObject, UpdateProcessQuery query, int? ifMatch)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for ID: {query.Id}");
            var existingProcess = await _dynamoDbContext.LoadAsync<ProcessesDb>(query.Id).ConfigureAwait(false);
            if (existingProcess == null) return null;
            if (ifMatch != existingProcess.VersionNumber)
                throw new VersionNumberConflictException(ifMatch, existingProcess.VersionNumber);

            var updatedProcess = CreateUpdatedProcess(existingProcess, requestObject, query);
            _logger.LogDebug($"Calling IDynamoDBContext.SaveAsync to update ID: {query.Id}");
            await _dynamoDbContext.SaveAsync(updatedProcess).ConfigureAwait(false);
            return updatedProcess.ToDomain();
        }
    }
}
