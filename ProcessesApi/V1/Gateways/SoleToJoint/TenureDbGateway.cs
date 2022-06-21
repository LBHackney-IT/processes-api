using System;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Hackney.Core.Logging;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Factories;
using Hackney.Shared.Tenure.Infrastructure;
using Microsoft.Extensions.Logging;
using Hackney.Shared.Person.Factories;
using Hackney.Shared.Tenure.Boundary.Requests;

namespace ProcessesApi.V1.Gateways
{
    public class TenureDbGateway : ITenureDbGateway
    {
        private readonly IDynamoDBContext _dynamoDbContext;
        private readonly ILogger<TenureDbGateway> _logger;

        public TenureDbGateway(IDynamoDBContext dynamoDbContext, ILogger<TenureDbGateway> logger)
        {
            _dynamoDbContext = dynamoDbContext;
            _logger = logger;
        }

        [LogCall]
        public async Task<TenureInformation> GetTenureById(Guid id)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for Tenure ID: {id}");

            var result = await _dynamoDbContext.LoadAsync<TenureInformationDb>(id).ConfigureAwait(false);
            return result?.ToDomain();
        }

        [LogCall]
        public async Task UpdateTenureById(TenureInformation tenureInformation)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.SaveAsync for id {tenureInformation.Id}");
            await _dynamoDbContext.SaveAsync(tenureInformation.ToDatabase()).ConfigureAwait(false);
        }

        [LogCall]
        public async Task<TenureInformationDb> PostNewTenureAsync(CreateTenureRequestObject createTenureRequestObject)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.SaveAsync");
            var tenureDbEntity = createTenureRequestObject.ToDatabase();

            await _dynamoDbContext.SaveAsync(tenureDbEntity).ConfigureAwait(false);

            return tenureDbEntity;
        }
    }
}
