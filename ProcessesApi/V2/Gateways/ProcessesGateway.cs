using Amazon.DynamoDBv2.DataModel;
using Amazon.DynamoDBv2.DocumentModel;
using Hackney.Core.DynamoDb;
using Hackney.Core.Logging;
using Microsoft.Extensions.Logging;
using ProcessesApi.V2.Boundary.Request;
using ProcessesApi.V2.Domain;
using ProcessesApi.V2.Factories;
using ProcessesApi.V2.Infrastructure;
using ProcessesApi.V2.UseCase.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ProcessesApi.V2.Gateways
{
    public class ProcessesGateway : IProcessesGateway
    {
        private const int MAX_RESULTS = 10;
        private const string GETPROCESSESBYTARGETIDINDEX = "ProcessesByTargetId";
        private const string TARGETID = "targetId";

        private readonly IDynamoDBContext _dynamoDbContext;
        private readonly IEntityUpdater _updater;
        private readonly ILogger<ProcessesGateway> _logger;

        public ProcessesGateway(IDynamoDBContext dynamoDbContext, IEntityUpdater updater, ILogger<ProcessesGateway> logger)
        {
            _dynamoDbContext = dynamoDbContext;
            _updater = updater;
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
    }
}
