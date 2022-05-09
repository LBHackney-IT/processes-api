using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hackney.Shared.Person;
using Hackney.Shared.Tenure.Domain;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Gateways.Exceptions;
using ProcessesApi.V1.Services.Exceptions;

namespace ProcessesApi.V1.Helpers
{
    public class SoleToJointAutomatedEligibilityChecksHelper : ISoleToJointAutomatedEligibilityChecksHelper
    {
        private readonly IIncomeApiGateway _incomeApiGateway;
        private readonly IPersonDbGateway _personDbGateway;
        private readonly ITenureDbGateway _tenureDbGateway;
        public Dictionary<string, bool> EligibilityResults { get; private set; }

        public SoleToJointAutomatedEligibilityChecksHelper(IIncomeApiGateway incomeApiGateway, IPersonDbGateway personDbGateway, ITenureDbGateway tenureDbGateway)
        {
            _incomeApiGateway = incomeApiGateway;
            _personDbGateway = personDbGateway;
            _tenureDbGateway = tenureDbGateway;
        }

        #region Automated eligibility checks

        /// <summary>
        /// Passes if the tenant is marked as a named tenure holder (tenure type is tenant)
        /// </summary>
        private static bool BR2(Guid tenantId, TenureInformation tenure)
        {
            var currentTenantDetails = tenure.HouseholdMembers.FirstOrDefault(x => x.Id == tenantId);
            if (currentTenantDetails is null)
                throw new FormDataInvalidException($"The tenant with ID {tenantId} is not listed as a household member of the tenure with ID {tenure.Id}");
            return currentTenantDetails.PersonTenureType == PersonTenureType.Tenant;
        }

        /// <summary>
        /// Passes if the tenant is not already part of a joint tenancy (there is not more than one responsible person)
        /// </summary>
        private static bool BR3(TenureInformation tenure) => tenure.HouseholdMembers.Count(x => x.IsResponsible) <= 1;

        /// <summary>
        /// Passes if the tenure is secure
        /// </summary>
        private static bool BR4(TenureInformation tenure) => tenure.TenureType.Code == TenureTypes.Secure.Code;

        /// <summary>
        /// Passes if the tenure is active
        /// </summary>
        private static bool BR6(TenureInformation tenure) => tenure.IsActive;

        /// <summary>
        /// Passes if there are no active payment agreeements on the tenure
        /// </summary>
        public static async Task<bool> BR7(TenureInformation tenure, IIncomeApiGateway gateway)
        {
            var tenancyRef = tenure.LegacyReferences.FirstOrDefault(x => x.Name == "uh_tag_ref");

            if (tenancyRef is null) return true; // TODO: Confirm error message
            var paymentAgreements = await gateway.GetPaymentAgreementsByTenancyReference(tenancyRef.Value, Guid.NewGuid())
                                                 .ConfigureAwait(false);

            return paymentAgreements == null || !paymentAgreements.Agreements.Any(x => x.Amount > 0);
        }

        /// <summary>
        /// Passes if there is no active NOSP (notice of seeking possession) on the tenure
        /// </summary>
        public static async Task<bool> BR8(TenureInformation tenure, IIncomeApiGateway gateway)
        {
            var tenancyRef = tenure.LegacyReferences.FirstOrDefault(x => x.Name == "uh_tag_ref");
            if (tenancyRef is null) return true; // TODO: Confirm error message
            var tenancy = await gateway.GetTenancyByReference(tenancyRef.Value, Guid.NewGuid())
                                       .ConfigureAwait(false);

            return tenancy == null || !tenancy.nosp.active;
        }

        /// <summary>
        /// Passes if the proposed tenant is not a minor
        /// </summary>
        private static bool BR19(Person proposedTenant) => !proposedTenant.IsAMinor ?? false;

        /// <summary>
        /// Passes if the proposed tenant does not have any active tenures (other than the selected tenure) that are not non-secure
        /// </summary>
        private static bool BR9(Person proposedTenant, Guid tenureId) => !proposedTenant.Tenures.Any(x => x.IsActive && x.Type != TenureTypes.NonSecure.Code && x.Id != tenureId);

        public async Task<bool> CheckAutomatedEligibility(Guid tenureId, Guid proposedTenantId, Guid tenantId)
        {
            var tenure = await _tenureDbGateway.GetTenureById(tenureId).ConfigureAwait(false);
            if (tenure is null) throw new TenureNotFoundException(tenureId);

            var proposedTenant = await _personDbGateway.GetPersonById(proposedTenantId).ConfigureAwait(false);
            if (proposedTenant is null) throw new PersonNotFoundException(proposedTenantId);

            EligibilityResults = new Dictionary<string, bool>()
            {
                { "BR2", BR2(tenantId, tenure) },
                { "BR3", BR3(tenure) },
                { "BR4", BR4(tenure) },
                { "BR6", BR6(tenure) },
                { "BR7", await BR7(tenure, _incomeApiGateway).ConfigureAwait(false) },
                { "BR8", await BR8(tenure, _incomeApiGateway).ConfigureAwait(false) },
                { "BR19", BR19(proposedTenant) },
                { "BR9", BR9(proposedTenant, tenureId) }
            };

            return !EligibilityResults.Any(x => x.Value == false);
        }

        #endregion
    }
}
