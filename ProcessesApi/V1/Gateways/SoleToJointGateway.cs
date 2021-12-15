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

namespace ProcessesApi.V1.Gateways
{
    public class SoleToJointGateway : ISoleToJointGateway
    {
        private readonly IDynamoDBContext _dynamoDbContext;
        private readonly ILogger<SoleToJointGateway> _logger;

        public SoleToJointGateway(IDynamoDBContext dynamoDbContext, ILogger<SoleToJointGateway> logger)
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

        private async Task<Person> GetPersonById(Guid id)
        {
            _logger.LogDebug($"Calling IDynamoDBContext.LoadAsync for Person ID: {id}");

            var result = await _dynamoDbContext.LoadAsync<PersonDbEntity>(id).ConfigureAwait(false);
            return result?.ToDomain();
        }

        public async Task<bool> CheckEligibility(Guid tenureId, Guid proposedTenantId)
        {
            var currentTenure = await GetTenureById(tenureId).ConfigureAwait(false);

            if (currentTenure is null)
                return false; // TODO: Confirm whether should raise error 

            var tenantInformation = currentTenure.HouseholdMembers.ToListOrEmpty().Find(x => x.Id == proposedTenantId);

            if (tenantInformation.PersonTenureType != PersonTenureType.Tenant
                || currentTenure.TenureType.Code != TenureTypes.Secure.Code
                || !currentTenure.IsActive
                || tenantInformation.DateOfBirth.AddYears(18) > DateTime.UtcNow)
            {
                return false;
            }
            else
            {
                var proposedTenant = await GetPersonById(proposedTenantId).ConfigureAwait(false);

                foreach (var x in proposedTenant.Tenures)
                {
                    var personTenure = await GetTenureById(x.Id).ConfigureAwait(false);

                    if (personTenure.TenureType.Code != TenureTypes.Secure.Code)
                        return false;

                    var personHouseholdMemberRecord = personTenure.HouseholdMembers.ToListOrEmpty().Find(x => x.Id == proposedTenantId);
                    if (personHouseholdMemberRecord is null)
                        break;
                    if (personTenure.HouseholdMembers.Count(x => x.IsResponsible) > 1)
                        return false;
                }

                return true;
            }
        }

    }
}
