using System;
using System.Collections.Generic;
using System.Linq;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.UseCase.Exceptions;
using Stateless;

namespace ProcessesApi.V1.Helpers
{
    public static class SoleToJointHelpers
    {
        public static UpdateProcessState ValidateManualCheck(StateMachine<string, string>.Transition transition,
                                               string passedTrigger,
                                               string failedTrigger,
                                               params (string CheckId, string Value)[] expectations)
        {
            var processRequest = (UpdateProcessState) transition.Parameters[0];
            var formData = processRequest.FormData;

            var expectedFormDataKeys = expectations.Select(expectation => expectation.CheckId).ToList();
            ValidateFormData(formData, expectedFormDataKeys);

            var isCheckPassed = expectations.All(expectation =>
                String.Equals(
                    expectation.Value,
                    formData[expectation.CheckId].ToString(),
                    StringComparison.OrdinalIgnoreCase));

            processRequest.Trigger = isCheckPassed
                ? passedTrigger
                : failedTrigger;

            return processRequest;
        }

        public static void ValidateFormData(Dictionary<string, object> requestFormData, List<string> expectedFormDataKeys)
        {
            expectedFormDataKeys.ForEach(x =>
            {
                if (!requestFormData.ContainsKey(x))
                    throw new FormDataNotFoundException(requestFormData.Keys.ToList(), expectedFormDataKeys);
            });
        }
    }
}
