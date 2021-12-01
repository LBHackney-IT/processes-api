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
using ProcessesApi.V1.Domain.SoleToJoint;

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
        public async Task<SoleToJointProcess> GetProcessById(Guid id)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for ID: {id}");

            var result = await _dynamoDbContext.LoadAsync<ProcessesDb>(id).ConfigureAwait(false);
            return result?.ToDomain();
        }

        [LogCall]
        public async Task<SoleToJointProcess> SaveProcess(SoleToJointProcess query)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.SaveAsync for id {query.Id}");
            var processDbEntity = query.ToDatabase();

            await _dynamoDbContext.SaveAsync(processDbEntity).ConfigureAwait(false);
            return processDbEntity.ToDomain();
        }

    }
}
