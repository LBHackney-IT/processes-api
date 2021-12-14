using System;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Hackney.Core.Logging;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Factories;
using Hackney.Shared.Tenure.Infrastructure;
using Microsoft.Extensions.Logging;

namespace ProcessesApi.V1.Helpers
{
    public class SoleToJointHelper : ISoleToJointHelper
    {
        private readonly IDynamoDBContext _dynamoDbContext;
        private readonly ILogger<SoleToJointHelper> _logger;

        public SoleToJointHelper(IDynamoDBContext dynamoDbContext, ILogger<SoleToJointHelper> logger)
        {
            _dynamoDbContext = dynamoDbContext;
            _logger = logger;
        }

        [LogCall]
        private async Task<TenureInformation> GetTenureById(Guid id)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for Tenure ID: {id}");

            var result = await _dynamoDbContext.LoadAsync<TenureInformationDb>(id).ConfigureAwait(false);
            return result?.ToDomain();
        }

        public async Task<bool> CheckEligibility(Guid tenureId, Guid incomingTenantId)
        {
            var tenure = await GetTenureById(tenureId).ConfigureAwait(false);
            var tenantInformation = tenure.HouseholdMembers.ToListOrEmpty().Find(x => x.Id == incomingTenantId);

            if(tenantInformation.PersonTenureType != PersonTenureType.Tenant)
            {
                return false; //AutomaticChecksFailed
            }
            else
            {
                return true; //AutomaticChecksPassed
            }
        }

    }
}