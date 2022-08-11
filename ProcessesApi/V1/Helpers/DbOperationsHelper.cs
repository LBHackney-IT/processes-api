using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hackney.Core.JWT;
using Hackney.Core.Sns;
using Hackney.Shared.Person;
using Hackney.Shared.Tenure.Boundary.Requests;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Factories;
using Hackney.Shared.Tenure.Infrastructure;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Constants.SoleToJoint;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Gateways.Exceptions;
using ProcessesApi.V1.Infrastructure;
using ProcessesApi.V1.Services.Exceptions;
using ProcessesApi.V1.Constants.ChangeOfName;
using Hackney.Shared.Person.Domain;
using Hackney.Shared.Person.Factories;
using Hackney.Shared.Person.Boundary.Request;
using ProcessesApi.V1.Boundary.Request;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace ProcessesApi.V1.Helpers
{
    public class DbOperationsHelper : IDbOperationsHelper
    {
        private readonly IIncomeApiGateway _incomeApiGateway;
        private readonly IPersonDbGateway _personDbGateway;
        private readonly ITenureDbGateway _tenureDbGateway;
        private readonly ITenureSnsFactory _tenureSnsFactory;
        private readonly IPersonSnsFactory _personSnsFactory;
        private readonly ISnsGateway _snsGateway;
        private Token _token;

        public Dictionary<string, bool> EligibilityResults { get; private set; }

        public DbOperationsHelper(IIncomeApiGateway incomeApiGateway,
                                             IPersonDbGateway personDbGateway,
                                             ITenureDbGateway tenureDbGateway,
                                             ITenureSnsFactory tenureSnsFactory,
                                             IPersonSnsFactory personSnsFactory,
                                             ISnsGateway snsGateway)
        {
            _incomeApiGateway = incomeApiGateway;
            _personDbGateway = personDbGateway;
            _tenureDbGateway = tenureDbGateway;
            _tenureSnsFactory = tenureSnsFactory;
            _personSnsFactory = personSnsFactory;
            _snsGateway = snsGateway;
        }

        public async Task AddIncomingTenantToRelatedEntities(Dictionary<string, object> requestFormData, Process process)
        {
            requestFormData.ValidateKeys(new List<string>() { SoleToJointKeys.IncomingTenantId });
            var incomingTenantId = Guid.Parse(requestFormData[SoleToJointKeys.IncomingTenantId].ToString());

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
            var request = new EditTenureDetailsRequestObject
            {
                StartOfTenureDate = tenure.StartOfTenureDate,
                EndOfTenureDate = DateTime.UtcNow,
                TenureType = tenure.TenureType
            };

            var result = await _tenureDbGateway.UpdateTenureById(tenure.Id, request).ConfigureAwait(false);
            if (result is null) throw new TenureNotFoundException(tenure.Id);

            var message = _tenureSnsFactory.UpdateTenure(result, _token);
            var topicArn = Environment.GetEnvironmentVariable("TENURE_SNS_ARN");
            await _snsGateway.Publish(message, topicArn).ConfigureAwait(false);
        }

        private async Task<TenureInformation> CreateNewTenure(TenureInformation oldTenure, Guid incomingTenantId)
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
            var householdMember = request.HouseholdMembers.Find(x => x.Id == incomingTenantId);
            if (householdMember is null) throw new Exception("Not household member");
            householdMember.PersonTenureType = PersonTenureType.Tenant;
            householdMember.IsResponsible = true;

            var result = await _tenureDbGateway.PostNewTenureAsync(request).ConfigureAwait(false);

            var tenureCreatedMessage = _tenureSnsFactory.CreateTenure(result, _token);
            var topicArn = Environment.GetEnvironmentVariable("TENURE_SNS_ARN");
            await _snsGateway.Publish(tenureCreatedMessage, topicArn).ConfigureAwait(false);

            return result.ToDomain();
        }

        private async Task UpdatePersonRecords(TenureInformation tenure)
        {
            var oldHouseholdMembers = new List<HouseholdMembers>();

            foreach (var householdMember in tenure.HouseholdMembers)
            {
                var updatedResult = new UpdateEntityResult<TenureInformationDb>()
                {
                    UpdatedEntity = tenure.ToDatabase(),
                    OldValues = new Dictionary<string, object> { { "householdMembers", oldHouseholdMembers } },
                    NewValues = new Dictionary<string, object> { { "householdMembers", new List<HouseholdMembers> { householdMember } } }
                };

                var personAddedMessage = _tenureSnsFactory.PersonAddedToTenure(updatedResult, _token);
                var topicArn = Environment.GetEnvironmentVariable("TENURE_SNS_ARN");
                await _snsGateway.Publish(personAddedMessage, topicArn).ConfigureAwait(false);

                oldHouseholdMembers.Add(householdMember);
            }
        }

        public async Task<Guid> UpdateTenures(Process process, Token token)
        {
            _token = token;

            var incomingTenant = process.RelatedEntities.Find(x => x.SubType == SubType.householdMember);
            var existingTenure = await _tenureDbGateway.GetTenureById(process.TargetId).ConfigureAwait(false);

            await EndExistingTenure(existingTenure).ConfigureAwait(false);
            var newTenure = await CreateNewTenure(existingTenure, incomingTenant.Id).ConfigureAwait(false);
            await UpdatePersonRecords(newTenure).ConfigureAwait(false);

            return newTenure.Id;
        }

        #endregion

        #region Update Name

        public async Task UpdatePerson(Process process, Token token)
        {
            _token = token;

            var existingPerson = await _personDbGateway.GetPersonById(process.TargetId).ConfigureAwait(false);
            if (existingPerson == null) throw new PersonNotFoundException(process.TargetId);

            var nameSubmitted = process.PreviousStates.Find(x => x.State == ChangeOfNameStates.NameSubmitted).ProcessData.FormData;
            var personRequestObject = new UpdatePersonRequestObject();

                personRequestObject.Title = (Title?) Enum.Parse(typeof(Title), nameSubmitted.GetValueOrDefault(ChangeOfNameKeys.Title).ToString());
                personRequestObject.FirstName = nameSubmitted.GetValueOrDefault(ChangeOfNameKeys.FirstName).ToString();
                personRequestObject.Surname = nameSubmitted.GetValueOrDefault(ChangeOfNameKeys.Surname).ToString();

            if (nameSubmitted.ContainsKey(ChangeOfNameKeys.MiddleName))
                personRequestObject.MiddleName = nameSubmitted.GetValueOrDefault(ChangeOfNameKeys.MiddleName).ToString();

            var result = await _personDbGateway.UpdatePersonByIdAsync(process.TargetId, personRequestObject).ConfigureAwait(false);

            var message = _personSnsFactory.Update(result, _token);
            var topicArn = Environment.GetEnvironmentVariable("PERSON_SNS_ARN");
            await _snsGateway.Publish(message, topicArn).ConfigureAwait(false);
        }

        #endregion
    }
}
