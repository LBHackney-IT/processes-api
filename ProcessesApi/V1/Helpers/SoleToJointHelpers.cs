using System;
using System.Collections.Generic;
using System.Linq;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Services.Exceptions;

namespace ProcessesApi.V1.Helpers
{
    public static class SoleToJointHelpers
    {
        public static Dictionary<string, object> _eventData;

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

        public static void ValidateHasNotifiedResident(this ProcessTrigger processRequest)
        {
            var formData = processRequest.FormData;

            ValidateFormData(formData, new List<string>() { SoleToJointFormDataKeys.HasNotifiedResident });

            if (formData.ContainsKey(SoleToJointFormDataKeys.Reason))
                _eventData = CreateEventData(formData, new List<string> { SoleToJointFormDataKeys.Reason });

            var hasNotifiedResidentString = processRequest.FormData[SoleToJointFormDataKeys.HasNotifiedResident];

            if (Boolean.TryParse(hasNotifiedResidentString.ToString(), out bool hasNotifiedResident))
            {
                if (!hasNotifiedResident) throw new FormDataInvalidException("Housing Officer must notify the resident before closing this process.");
            }
            else
            {
                throw new FormDataFormatException("boolean", hasNotifiedResidentString);
            }
        }
    }
}
