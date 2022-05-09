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
        public Dictionary<string, bool> EligibilityResults { get; private set; }
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
        private async Task<PaymentAgreements> GetPaymentAgreementsByTenancyReference(string tenancyRef, Guid correlationId)
        {
            _logger.LogDebug($"Calling Income API for payment agreement with tenancy ref: {tenancyRef}");
            var route = $"{_apiGateway.ApiRoute}/agreements/{tenancyRef}";
            return await _apiGateway.GetByIdAsync<PaymentAgreements>(route, tenancyRef, correlationId);
        }

        [LogCall]
        private async Task<Tenancy> GetTenancyByReference(string tenancyRef, Guid correlationId)
        {
            _logger.LogDebug($"Calling Income API with tenancy ref: {tenancyRef}");
            var route = $"{_apiGateway.ApiRoute}/tenancies/{tenancyRef}";
            return await _apiGateway.GetByIdAsync<Tenancy>(route, tenancyRef, correlationId);
        }

        /// <summary>
        /// Passes if the tenant is marked as a named tenure holder (tenure type is tenant)
        /// </summary>
        private bool BR2(Guid tenantId, TenureInformation tenure)
        {
            var currentTenantDetails = tenure.HouseholdMembers.FirstOrDefault(x => x.Id == tenantId);
            return currentTenantDetails.PersonTenureType == PersonTenureType.Tenant;
        }

        /// <summary>
        /// Passes if the tenant is not already part of a joint tenancy (there is not more than one responsible person)
        /// </summary>
        private bool BR3(TenureInformation tenure)
        {
            return tenure.HouseholdMembers.Count(x => x.IsResponsible) <= 1;
        }

        /// <summary>
        /// Passes if the tenure is secure
        /// </summary>
        private bool BR4(TenureInformation tenure)
        {
            return tenure.TenureType.Code == TenureTypes.Secure.Code;
        }

        /// <summary>
        /// Passes if the tenure is active
        /// </summary>
        private bool BR6(TenureInformation tenure)
        {
            return tenure.IsActive;
        }


        ///// <summary>
        ///// Temporarily moved to Manual check
        ///// Passes if there are no active payment agreeements on the tenure
        ///// </summary>
        //private async Task<bool> BR7(TenureInformation tenure)
        //{
        //    var tenancyRef = tenure.LegacyReferences.FirstOrDefault(x => x.Name == "uh_tag_ref");

        //    if (tenancyRef is null) return true;
        //    var paymentAgreements = await GetPaymentAgreementsByTenancyReference(tenancyRef.Value, Guid.NewGuid())
        //                                .ConfigureAwait(false);

        //    return paymentAgreements == null || !paymentAgreements.Agreements.Any(x => x.Amount > 0);
        //}

        ///// <summary>
        ///// Temporarily moved to Manual check
        ///// Passes if there is no active NOSP (notice of seeking possession) on the tenure
        ///// </summary>
        //private async Task<bool> BR8(TenureInformation tenure)
        //{
        //    var tenancyRef = tenure.LegacyReferences.FirstOrDefault(x => x.Name == "uh_tag_ref");
        //    if (tenancyRef is null) return true;
        //    var tenancy = await GetTenancyByReference(tenancyRef.Value, Guid.NewGuid())
        //                        .ConfigureAwait(false);

        //    return tenancy == null || !tenancy.nosp.active;
        //}

        /// <summary>
        /// Passes if the proposed tenant is not a minor
        /// </summary>
        private bool BR19(Person proposedTenant)
        {
            return !proposedTenant.IsAMinor ?? false;
        }

        /// <summary>
        /// Passes if the proposed tenant does not have any active tenures (other than the selected tenure) that are not non-secure
        /// </summary>
        private bool BR9(Person proposedTenant, Guid tenureId)
        {
            return !proposedTenant.Tenures.Any(x => x.IsActive && x.Type != TenureTypes.NonSecure.Code && x.Id != tenureId);
        }

        public async Task<bool> CheckEligibility(Guid tenureId, Guid proposedTenantId, Guid tenantId)
        {
            var tenure = await GetTenureById(tenureId).ConfigureAwait(false);
            if (tenure is null) throw new TenureNotFoundException(tenureId);

            var proposedTenant = await GetPersonById(proposedTenantId).ConfigureAwait(false);
            if (proposedTenant is null) throw new PersonNotFoundException(proposedTenantId);

            EligibilityResults = new Dictionary<string, bool>()
            {
                { "BR2", BR2(tenantId, tenure) },
                { "BR3", BR3(tenure) },
                { "BR4", BR4(tenure) },
                { "BR6", BR6(tenure) },
                //{ "BR7", await BR7(tenure).ConfigureAwait(false) },
                //{ "BR8", await BR8(tenure).ConfigureAwait(false) },
                { "BR19", BR19(proposedTenant) },
                { "BR9", BR9(proposedTenant, tenureId) }
            };

            return !EligibilityResults.Any(x => x.Value == false);
        }

    }
}
