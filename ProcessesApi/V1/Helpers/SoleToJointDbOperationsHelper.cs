using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hackney.Shared.Person;
using Hackney.Shared.Person.Boundary.Request;
using Hackney.Shared.Person.Boundary.Response;
using Hackney.Shared.Person.Domain;
using Hackney.Shared.Tenure.Boundary.Requests;
using Hackney.Shared.Tenure.Domain;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Gateways.Exceptions;
using ProcessesApi.V1.Services.Exceptions;

namespace ProcessesApi.V1.Helpers
{
    public class SoleToJointDbOperationsHelper : ISoleToJointDbOperationsHelper
    {
        private readonly IIncomeApiGateway _incomeApiGateway;
        private readonly IPersonDbGateway _personDbGateway;
        private readonly ITenureDbGateway _tenureDbGateway;
        private readonly IPersonApiGateway _personApiGateway;
        private readonly ITenureApiGateway _tenureApiGateway;

        public Dictionary<string, bool> EligibilityResults { get; private set; }

        public SoleToJointDbOperationsHelper(IIncomeApiGateway incomeApiGateway,
                                             IPersonDbGateway personDbGateway,
                                             ITenureDbGateway tenureDbGateway,
                                             IPersonApiGateway personApiGateway,
                                             ITenureApiGateway tenureApiGateway)
        {
            _incomeApiGateway = incomeApiGateway;
            _personDbGateway = personDbGateway;
            _tenureDbGateway = tenureDbGateway;
            _personApiGateway = personApiGateway;
            _tenureApiGateway = tenureApiGateway;
        }

        public async Task AddIncomingTenantToRelatedEntities(Dictionary<string, object> requestFormData, Process process)
        {
            SoleToJointHelpers.ValidateFormData(requestFormData, new List<string>() { SoleToJointFormDataKeys.IncomingTenantId });

            //TODO: When doing a POST request from the FE they should created a relatedEntities object with all neccesary values
            // Once Frontend work is completed the code below should be removed.
            if (process.RelatedEntities == null)
                process.RelatedEntities = new List<RelatedEntity>();

            var incomingTenantId = Guid.Parse(requestFormData[SoleToJointFormDataKeys.IncomingTenantId].ToString());

            var incomingTenant = await _personDbGateway.GetPersonById(incomingTenantId).ConfigureAwait(false);
            if (incomingTenant is null) throw new PersonNotFoundException(incomingTenantId);

            var relatedEntity = new RelatedEntity()
            {
                Id = incomingTenantId,
                TargetType = TargetType.person,
                SubType = SubType.householdMember,
                Description = $"{incomingTenant.FirstName} {incomingTenant.Surname}"
            };

            process.RelatedEntities.Add(relatedEntity);
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

            return tenancy == null || !tenancy.NOSP.Active;
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
                { "BR7", true }, // await BR7(tenure, _incomeApiGateway).ConfigureAwait(false) - Check has been temporarily moved to a Manual Eligibility Check
                { "BR8", true }, // await BR8(tenure, _incomeApiGateway).ConfigureAwait(false) - Check has been temporarily moved to a Manual Eligibility Check
                { "BR19", BR19(proposedTenant) },
                { "BR9", BR9(proposedTenant, tenureId) }
            };

            return !EligibilityResults.Any(x => x.Value == false);
        }

        #endregion

        #region Update tenures

        private async Task EndExistingTenure(TenureInformation tenure)
        {
            var request = new EditTenureDetailsRequestObject { EndOfTenureDate = DateTime.UtcNow };
            await _tenureApiGateway.EditTenureDetailsById(tenure.Id, request, tenure.VersionNumber).ConfigureAwait(false);
        }

        private async Task<TenureResponseObject> CreateNewTenureWithIncomingTenant(TenureInformation oldTenure, Guid incomingTenantId)
        {
            var request = new CreateTenureRequestObject()
            {
                Notices = oldTenure.Notices.ToList(),
                SubletEndDate = oldTenure.SubletEndDate,
                PotentialEndDate = oldTenure.PotentialEndDate,
                EvictionDate = oldTenure.EvictionDate,
                SuccessionDate = oldTenure.SuccessionDate,
                LegacyReferences = oldTenure.LegacyReferences.ToList(),
                Terminated = oldTenure.Terminated,
                TenureType = oldTenure.TenureType,
                EndOfTenureDate = oldTenure.EndOfTenureDate,
                Charges = oldTenure.Charges,
                TenuredAsset = oldTenure.TenuredAsset,
                HouseholdMembers = oldTenure.HouseholdMembers.ToList(),
                PaymentReference = oldTenure.PaymentReference,
                AgreementType = oldTenure.AgreementType
            };

            if (oldTenure.IsSublet.HasValue) request.IsSublet = oldTenure.IsSublet.Value;
            if (oldTenure.InformHousingBenefitsForChanges.HasValue) request.InformHousingBenefitsForChanges = oldTenure.InformHousingBenefitsForChanges.Value;
            if (oldTenure.IsMutualExchange.HasValue) request.IsMutualExchange = oldTenure.IsMutualExchange.Value;
            if (oldTenure.IsTenanted.HasValue) request.IsTenanted = oldTenure.IsTenanted.Value;

            request.StartOfTenureDate = DateTime.UtcNow;

            var incomingTenantHouseholdMember = request.HouseholdMembers.Find(x => x.Id == incomingTenantId);
            if (incomingTenantHouseholdMember is null)
                throw new Exception($"The tenant with ID {incomingTenantId} is not listed as a household member of the tenure with ID {oldTenure.Id}");
            incomingTenantHouseholdMember.PersonTenureType = PersonTenureType.Tenant;
            incomingTenantHouseholdMember.IsResponsible = true;

            var result = await _tenureApiGateway.CreateNewTenure(request).ConfigureAwait(false);
            return result;
        }

        public async Task<Guid> UpdateTenures(Process process)
        {
            var incomingTenant = process.RelatedEntities.Find(x => x.SubType == SubType.householdMember);
            var existingTenure = await _tenureDbGateway.GetTenureById(process.TargetId).ConfigureAwait(false);

            await EndExistingTenure(existingTenure).ConfigureAwait(false);
            var newTenure = await CreateNewTenureWithIncomingTenant(existingTenure, incomingTenant.Id).ConfigureAwait(false);

            return newTenure.Id;
        }

        #endregion
    }
}
