using System;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Hackney.Shared.Tenure.Factories;
using Microsoft.Extensions.Logging;
using Hackney.Shared.Person.Infrastructure;
using Hackney.Shared.Person.Factories;
using Hackney.Shared.Person;

namespace ProcessesApi.V1.Gateways
{
    public class PersonDbGateway : IPersonDbGateway
    {
        private readonly IDynamoDBContext _dynamoDbContext;
        private readonly ILogger<PersonDbGateway> _logger;

        public PersonDbGateway(IDynamoDBContext dynamoDbContext, ILogger<PersonDbGateway> logger)
        {
            _dynamoDbContext = dynamoDbContext;
            _logger = logger;
        }

        public async Task<Person> GetPersonById(Guid id)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for Person ID: {id}");

            var result = await _dynamoDbContext.LoadAsync<PersonDbEntity>(id).ConfigureAwait(false);
            return result?.ToDomain();
        }

        public async Task<Person> UpdatePersonById(Person person)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.SaveAsync for Person ID: {person.Id}");
            await _dynamoDbContext.SaveAsync(person.ToDatabase()).ConfigureAwait(false);
            return person;
        }
    }
}
