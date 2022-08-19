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
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure;
using System.Text.Json;
using System.Linq;
using System.Text.Json.Serialization;

namespace ProcessesApi.V1.Gateways
{
    public class TenureDbGateway : ITenureDbGateway
    {
        private readonly IDynamoDBContext _dynamoDbContext;
        private readonly ILogger<TenureDbGateway> _logger;
        private readonly IEntityUpdater _updater;

        public TenureDbGateway(IDynamoDBContext dynamoDbContext, ILogger<TenureDbGateway> logger, IEntityUpdater updater)
        {
            _dynamoDbContext = dynamoDbContext;
            _logger = logger;
            _updater = updater;
        }

        [LogCall]
        public async Task<TenureInformation> GetTenureById(Guid id)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for Tenure ID: {id}");

            var result = await _dynamoDbContext.LoadAsync<TenureInformationDb>(id).ConfigureAwait(false);
            return result?.ToDomain();
        }

        [LogCall]
        public async Task<UpdateEntityResult<TenureInformationDb>> UpdateTenureById(Guid id, EditTenureDetailsRequestObject updateTenureRequest)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for tenure id {id}");

            var existingTenure = await _dynamoDbContext.LoadAsync<TenureInformationDb>(id).ConfigureAwait(false);
            if (existingTenure == null) return null;

            var requestBody = JsonSerializer.Serialize(updateTenureRequest, GatewayHelpers.GetJsonSerializerOptions());
            var response = _updater.UpdateEntity(existingTenure, requestBody, updateTenureRequest);

            if (response.NewValues.Any())
            {
                _logger.LogDebug($"Calling IDynamoDBContext.SaveAsync to update tenure id {id}");
                await _dynamoDbContext.SaveAsync<TenureInformationDb>(response.UpdatedEntity).ConfigureAwait(false);
            }

            return response;
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
