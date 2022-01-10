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
using ProcessesApi.V1.Domain.Finance;
using ProcessesApi.V1.Gateways.Exceptions;

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
            _apiGateway.Initialise(ApiName, IncomeApiUrl, IncomeApiToken);
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

        [LogCall]
        private async Task<PaymentAgreements> GetPaymentAgreementsByTenureId(Guid tenureId, Guid correlationId)
        {
            _logger.LogDebug($"Calling Income API for payment agreeement with Tenure ID: {tenureId}");
            var route = $"{_apiGateway.ApiRoute}/agreements/{tenureId}";
            return await _apiGateway.GetByIdAsync<PaymentAgreements>(route, tenureId, correlationId);
        }

        [LogCall]
        private async Task<Tenancy> GetTenancyById(Guid id, Guid correlationId)
        {
            _logger.LogDebug($"Calling Income API with tenancy ID: {id}");
            var route = $"{_apiGateway.ApiRoute}/tenancies/{id}";
            return await _apiGateway.GetByIdAsync<Tenancy>(route, id, correlationId);
        }

        public async Task<bool> CheckTenureFinanceRecords(Guid tenureId)
        {
            var paymentAgreements = await GetPaymentAgreementsByTenureId(tenureId, Guid.NewGuid()).ConfigureAwait(false); // TODO: Confirm what correlation ID to use
            if (paymentAgreements != null && paymentAgreements.Agreements.Count(x => x.Amount > 0) > 0)
            {
                return false;
            }
            else
            {
                var tenancy = await GetTenancyById(tenureId, Guid.NewGuid()).ConfigureAwait(false); // TODO: Confirm what correlation ID to use
                if (tenancy is null)
                    return true;

                if (tenancy.nosp.active)
                    return false;

                return true;
            }

        }

        public async Task<bool> CheckPersonTenureRecord(Guid tenureId, Guid proposedTenantId)
        {
            var tenure = await GetTenureById(tenureId).ConfigureAwait(false);
            if (tenure is null)
                return true; // skips this tenure if it doesn't exist

            var personHouseholdMemberRecord = tenure.HouseholdMembers.ToListOrEmpty().Find(x => x.Id == proposedTenantId);
            if (personHouseholdMemberRecord is null)
                return true; // skips this tenure if the person isn't listed as a household member

            if (tenure.TenureType.Code != TenureTypes.Secure.Code ||
                (tenure.HouseholdMembers.Count(x => x.IsResponsible) > 1 && personHouseholdMemberRecord.IsResponsible))
            {
                return false;
            }
            else
            {
                return await CheckTenureFinanceRecords(tenureId).ConfigureAwait(false);
            }
        }

        public async Task<bool> CheckEligibility(Guid tenureId, Guid proposedTenantId)
        {
            var currentTenure = await GetTenureById(tenureId).ConfigureAwait(false);
            if (currentTenure is null)
                throw new TenureNotFoundException(tenureId);

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
                if (proposedTenant is null)
                    throw new PersonNotFoundException(proposedTenantId);

                foreach (var x in proposedTenant.Tenures.Where(x => x.IsActive))
                {
                    var isEligible = await CheckPersonTenureRecord(x.Id, proposedTenantId).ConfigureAwait(false);
                    if (!isEligible)
                        return false;
                }
                return true;
            }
        }

    }
}
