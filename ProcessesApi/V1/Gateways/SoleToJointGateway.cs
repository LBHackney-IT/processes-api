using System;
using System.Threading.Tasks;
using System.Linq;
using Amazon.DynamoDBv2.DataModel;
using Hackney.Core.Logging;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Factories;
using Hackney.Shared.Tenure.Infrastructure;
using Microsoft.Extensions.Logging;
using Hackney.Shared.Person.Infrastructure;
using Hackney.Shared.Person.Factories;
using Hackney.Shared.Person;
using Hackney.Core.Http;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Gateways.Exceptions;
using System.Collections.Generic;

namespace ProcessesApi.V1.Gateways
{
    public class SoleToJointGateway : ISoleToJointGateway
    {
        private readonly IDynamoDBContext _dynamoDbContext;
        private readonly ILogger<SoleToJointGateway> _logger;
        private const string ApiName = "Income";
        private const string IncomeApiUrl = "IncomeApiUrl";
        private const string IncomeApiToken = "IncomeApiToken";
        private readonly IApiGateway _apiGateway;

        public SoleToJointGateway(IDynamoDBContext dynamoDbContext, ILogger<SoleToJointGateway> logger, IApiGateway apiGateway)
        {
            _dynamoDbContext = dynamoDbContext;
            _logger = logger;
            _apiGateway = apiGateway;
            _apiGateway.Initialise(ApiName, IncomeApiUrl, IncomeApiToken, null, useApiKey: true);
        }

        [LogCall]
        public async Task<TenureInformation> GetTenureById(Guid id)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for Tenure ID: {id}");

            var result = await _dynamoDbContext.LoadAsync<TenureInformationDb>(id).ConfigureAwait(false);
            return result?.ToDomain();
        }

        public async Task<Person> GetPersonById(Guid id)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for Person ID: {id}");

            var result = await _dynamoDbContext.LoadAsync<PersonDbEntity>(id).ConfigureAwait(false);
            return result?.ToDomain();
        }

        [LogCall]
        public async Task<PaymentAgreements> GetPaymentAgreementsByTenancyReference(string tenancyRef, Guid correlationId)
        {
            _logger.LogDebug($"Calling Income API for payment agreement with tenancy ref: {tenancyRef}");
            var route = $"{_apiGateway.ApiRoute}/agreements/{tenancyRef}";
            return await _apiGateway.GetByIdAsync<PaymentAgreements>(route, tenancyRef, correlationId);
        }

        [LogCall]
        public async Task<Tenancy> GetTenancyByReference(string tenancyRef, Guid correlationId)
        {
            _logger.LogDebug($"Calling Income API with tenancy ref: {tenancyRef}");
            var route = $"{_apiGateway.ApiRoute}/tenancies/{tenancyRef}";
            return await _apiGateway.GetByIdAsync<Tenancy>(route, tenancyRef, correlationId);
        }
    }
}
