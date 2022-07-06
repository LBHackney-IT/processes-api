using System;
using System.Collections.Generic;
using System.Linq;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Services.Exceptions;
using ProcessesApi.V1.Constants;

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
            SharedHelper.ValidateFormData(formData, expectedFormDataKeys);

            var isCheckPassed = expectations.All(expectation =>
                String.Equals(expectation.Value,
                              formData[expectation.CheckId].ToString(),
                              StringComparison.OrdinalIgnoreCase)
            );

            processRequest.Trigger = isCheckPassed ? passedTrigger : failedTrigger;
        }

        public static void AddNewTenureToRelatedEntities(Guid newTenureId, Process process)
        {
            var relatedEntity = new RelatedEntity()
            {
                Id = newTenureId,
                TargetType = TargetType.tenure,
                SubType = SubType.newTenure,
                Description = "New Tenure created for this process."
            };
            process.RelatedEntities.Add(relatedEntity);
        }


        public static void ValidateRecommendation(this ProcessTrigger processRequest, Dictionary<string, string> triggerMappings, string keyName, List<string> otherExpectedFormDataKeys)
        {
            var formData = processRequest.FormData;

            var expectedFormDataKeys = otherExpectedFormDataKeys ?? new List<string>();
            expectedFormDataKeys.Add(keyName);
            SharedHelper.ValidateFormData(formData, expectedFormDataKeys);

            var recommendation = formData[keyName].ToString();

            if (!triggerMappings.ContainsKey(recommendation))
                throw new FormDataValueInvalidException(keyName, recommendation, triggerMappings.Keys.ToList());
            processRequest.Trigger = triggerMappings[recommendation];

        }

        public static Dictionary<string, object> ValidateHasNotifiedResident(this ProcessTrigger processRequest)
        {
            var formData = processRequest.FormData;
            SharedHelper.ValidateFormData(formData, new List<string>() { SharedKeys.HasNotifiedResident });

            var eventData = new Dictionary<string, object>();

            if (formData.ContainsKey(SharedKeys.Reason))
                eventData = SharedHelper.CreateEventData(formData, new List<string> { SharedKeys.Reason });

            var hasNotifiedResidentString = processRequest.FormData[SharedKeys.HasNotifiedResident];

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
    }
}
