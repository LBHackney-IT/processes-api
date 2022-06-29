using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Hackney.Shared.Person.Boundary.Request;
using Hackney.Shared.Tenure.Boundary.Requests;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Factories;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Gateways.Exceptions;
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
    }
}
