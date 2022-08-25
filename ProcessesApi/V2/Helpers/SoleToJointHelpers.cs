using System;
using System.Collections.Generic;
using System.Linq;
using ProcessesApi.V1.Services.Exceptions;
using ProcessesApi.V2.Domain;

namespace ProcessesApi.V2.Helpers
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
            formData.ValidateKeys(expectedFormDataKeys);

            var isCheckPassed = expectations.All(expectation =>
                String.Equals(expectation.Value,
                              formData[expectation.CheckId].ToString(),
                              StringComparison.OrdinalIgnoreCase)
            );

            processRequest.Trigger = isCheckPassed ? passedTrigger : failedTrigger;
        }

        public static void AddNewTenureToRelatedEntities(this Process process, Guid newTenureId)
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


        public static void SelectTriggerFromUserInput(this ProcessTrigger processRequest, Dictionary<string, string> triggerMappings, string recommendationKeyName, List<string> otherExpectedFormDataKeys)
        {
            var formData = processRequest.FormData;

            var expectedFormDataKeys = otherExpectedFormDataKeys ?? new List<string>();
            expectedFormDataKeys.Add(recommendationKeyName);
            formData.ValidateKeys(expectedFormDataKeys);

            var recommendation = formData[recommendationKeyName].ToString();

            if (!triggerMappings.ContainsKey(recommendation))
                throw new FormDataValueInvalidException(recommendationKeyName, recommendation, triggerMappings.Keys.ToList());
            processRequest.Trigger = triggerMappings[recommendation];
        }
    }
}
