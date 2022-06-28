using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hackney.Shared.Person;
using Hackney.Shared.Tenure.Boundary.Requests;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Factories;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Services.Exceptions;

namespace ProcessesApi.V1.Helpers
{
    public static class SoleToJointHelpers
    {
        public static void ValidateManualCheck(this ProcessTrigger processRequest,
                                               string passedTrigger,
                                               string failedTrigger,
                                               params (string CheckId, string Value)[] expectations)
        {
            var formData = processRequest.FormData;
            var expectedFormDataKeys = expectations.Select(expectation => expectation.CheckId).ToList();
            ValidateFormData(formData, expectedFormDataKeys);

            var isCheckPassed = expectations.All(expectation =>
                String.Equals(expectation.Value,
                              formData[expectation.CheckId].ToString(),
                              StringComparison.OrdinalIgnoreCase)
            );

            processRequest.Trigger = isCheckPassed ? passedTrigger : failedTrigger;
        }

        public static void ValidateFormData(Dictionary<string, object> requestFormData, List<string> expectedFormDataKeys)
        {
            expectedFormDataKeys.ForEach(x =>
            {
                if (!requestFormData.ContainsKey(x))
                    throw new FormDataNotFoundException(requestFormData.Keys.ToList(), expectedFormDataKeys);
            });
        }

        public static void AddIncomingTenantToRelatedEntities(Dictionary<string, object> requestFormData, Process process, IGetPersonByIdHelper personByIdHelper)
        {
            ValidateFormData(requestFormData, new List<string>() { SoleToJointFormDataKeys.IncomingTenantId });

            //TODO: When doing a POST request from the FE they should created a relatedEntities object with all neccesary values
            // Once Frontend work is completed the code below should be removed.
            if (process.RelatedEntities == null)
                process.RelatedEntities = new List<RelatedEntity>();

            var incomingTenantId = Guid.Parse(requestFormData[SoleToJointFormDataKeys.IncomingTenantId].ToString());

            var incomingTenant = personByIdHelper.GetPersonById(incomingTenantId).GetAwaiter().GetResult();
            var relatedEntity = new RelatedEntity()
            {
                Id = incomingTenantId,
                TargetType = TargetType.person,
                SubType = SubType.householdMember,
                Description = $"{incomingTenant.FirstName} {incomingTenant.Surname}"
            };
            process.RelatedEntities.Add(relatedEntity);
        }

        public static void AddNewTenureToRelatedEntities(TenureInformation newTenure, Process process)
        {
            var relatedEntity = new RelatedEntity()
            {
                Id = newTenure.Id,
                TargetType = TargetType.tenure,
                SubType = SubType.newTenure,
                Description = "New Tenure"
            };
            process.RelatedEntities.Add(relatedEntity);
        }

        public static Dictionary<string, object> CreateEventData(Dictionary<string, object> requestFormData, List<string> selectedKeys)
        {
            return requestFormData.Where(x => selectedKeys.Contains(x.Key))
                                  .ToDictionary(val => val.Key, val => val.Value);
        }

        public static void ValidateRecommendation(this ProcessTrigger processRequest, Dictionary<string, string> triggerMappings, string keyName, List<string> otherExpectedFormDataKeys)
        {
            var formData = processRequest.FormData;

            var expectedFormDataKeys = otherExpectedFormDataKeys ?? new List<string>();
            expectedFormDataKeys.Add(keyName);
            ValidateFormData(formData, expectedFormDataKeys);

            var recommendation = formData[keyName].ToString();

            if (!triggerMappings.ContainsKey(recommendation))
                throw new FormDataValueInvalidException(keyName, recommendation, triggerMappings.Keys.ToList());
            processRequest.Trigger = triggerMappings[recommendation];

        }

        public static Dictionary<string, object> ValidateHasNotifiedResident(this ProcessTrigger processRequest)
        {
            var formData = processRequest.FormData;
            ValidateFormData(formData, new List<string>() { SoleToJointFormDataKeys.HasNotifiedResident });

            var eventData = new Dictionary<string, object>();

            if (formData.ContainsKey(SoleToJointFormDataKeys.Reason))
                eventData = CreateEventData(formData, new List<string> { SoleToJointFormDataKeys.Reason });

            var hasNotifiedResidentString = processRequest.FormData[SoleToJointFormDataKeys.HasNotifiedResident];

            if (Boolean.TryParse(hasNotifiedResidentString.ToString(), out bool hasNotifiedResident))
            {
                if (!hasNotifiedResident)
                    throw new FormDataInvalidException("Housing Officer must notify the resident before closing this process.");
                return eventData;
            }
            else
            {
                throw new FormDataFormatException("boolean", hasNotifiedResidentString);
            }
        }

        private static async Task EndExistingTenure(TenureInformation tenure, ITenureDbGateway tenureDbGateway)
        {
            tenure.EndOfTenureDate = DateTime.UtcNow;
            await tenureDbGateway.UpdateTenureById(tenure).ConfigureAwait(false);
        }

        private static async Task<TenureInformation> AddIncomingTenantToNewTenure(TenureInformation oldTenure, Guid incomingTenantId, ITenureDbGateway tenureDbGateway)
        {
            var createTenureRequest = new CreateTenureRequestObject()
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

            if (oldTenure.IsSublet.HasValue) createTenureRequest.IsSublet = oldTenure.IsSublet.Value;
            if (oldTenure.InformHousingBenefitsForChanges.HasValue) createTenureRequest.InformHousingBenefitsForChanges = oldTenure.InformHousingBenefitsForChanges.Value;
            if (oldTenure.IsMutualExchange.HasValue) createTenureRequest.IsMutualExchange = oldTenure.IsMutualExchange.Value;
            if (oldTenure.StartOfTenureDate.HasValue) createTenureRequest.StartOfTenureDate = oldTenure.StartOfTenureDate.Value;
            if (oldTenure.IsTenanted.HasValue) createTenureRequest.IsTenanted = oldTenure.IsTenanted.Value;

            var incomingTenantHouseholdMember = createTenureRequest.HouseholdMembers.Find(x => x.Id == incomingTenantId);
            incomingTenantHouseholdMember.PersonTenureType = PersonTenureType.Tenant;

            var result = await tenureDbGateway.PostNewTenureAsync(createTenureRequest).ConfigureAwait(false);
            return result.ToDomain();
        }

        private static async Task AddNewTenureToPersonRecord(Guid oldTenureId, Guid newTenureId, Guid personId, IPersonDbGateway personDbGateway)
        {
            var person = await personDbGateway.GetPersonById(personId).ConfigureAwait(false);

            var tenures = person.Tenures.ToList();
            var tenureDetails = person.Tenures.First(x => x.Id == oldTenureId);
            tenureDetails.Id = newTenureId;

            person.Tenures = tenures;
            person.VersionNumber += 1;
            await personDbGateway.UpdatePersonById(person).ConfigureAwait(false);
        }

        public static async Task<TenureInformation> UpdateTenures(Process process, ITenureDbGateway tenureDbGateway, IPersonDbGateway personDbGateway)
        {
            var incomingTenant = process.RelatedEntities.Find(x => x.SubType == SubType.householdMember);
            var existingTenure = await tenureDbGateway.GetTenureById(process.TargetId).ConfigureAwait(false);

            await EndExistingTenure(existingTenure, tenureDbGateway).ConfigureAwait(false);
            var newTenure = await AddIncomingTenantToNewTenure(existingTenure, incomingTenant.Id, tenureDbGateway).ConfigureAwait(false);
            await AddNewTenureToPersonRecord(existingTenure.Id, newTenure.Id, incomingTenant.Id, personDbGateway).ConfigureAwait(false);

            return newTenure;
        }

    }
}
