using Amazon.DynamoDBv2.DataModel;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure;
using Hackney.Core.Logging;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;
using System;
using ProcessesApi.V1.Boundary.Request;

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
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for id parameter {id}");

            var result = await _dynamoDbContext.LoadAsync<ProcessesDb>(id).ConfigureAwait(false);
            return result?.ToDomain();
        }

        [LogCall]
        public async Task<Process> CreateNewProcess(CreateProcessQuery query)
        {
            _logger.LogDebug("Calling IDynamoDBContext.SaveAsync");
            var processDbEntity = query.ToDatabase();

            await _dynamoDbContext.SaveAsync(processDbEntity).ConfigureAwait(false);
            return processDbEntity.ToDomain();
        }
    }
}
