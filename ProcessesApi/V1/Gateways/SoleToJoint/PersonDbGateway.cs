using System;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Microsoft.Extensions.Logging;
using Hackney.Shared.Person.Infrastructure;
using Hackney.Shared.Person.Factories;
using Hackney.Shared.Person;
using ProcessesApi.V1.Infrastructure;
using Hackney.Shared.Person.Boundary.Request;
using System.Linq;
using System.Text.Json;

namespace ProcessesApi.V1.Gateways
{
    public class PersonDbGateway : IPersonDbGateway
    {
        private readonly IDynamoDBContext _dynamoDbContext;
        private readonly IEntityUpdater _updater;
        private readonly ILogger<PersonDbGateway> _logger;

        public PersonDbGateway(IDynamoDBContext dynamoDbContext, ILogger<PersonDbGateway> logger, IEntityUpdater updater)
        {
            _dynamoDbContext = dynamoDbContext;
            _logger = logger;
            _updater = updater;
        }

        public async Task<Person> GetPersonById(Guid id)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for Person ID: {id}");

            var result = await _dynamoDbContext.LoadAsync<PersonDbEntity>(id).ConfigureAwait(false);
            return result?.ToDomain();
        }

        public async Task<UpdateEntityResult<PersonDbEntity>> UpdatePersonByIdAsync(Guid id, UpdatePersonRequestObject updatePersonRequest)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for person id {id}");

            var existingPerson = await _dynamoDbContext.LoadAsync<PersonDbEntity>(id).ConfigureAwait(false);
            if (existingPerson == null) return null;

            var requestBody = JsonSerializer.Serialize(updatePersonRequest, GatewayHelpers.GetJsonSerializerOptions());
            var response = _updater.UpdateEntity(existingPerson, requestBody, updatePersonRequest);

            if (response.NewValues.Any())
            {
                _logger.LogDebug($"Calling IDynamoDBContext.SaveAsync to update person id {id}");
                await _dynamoDbContext.SaveAsync<PersonDbEntity>(response.UpdatedEntity).ConfigureAwait(false);
            }

            return response;
        }
    }
}
