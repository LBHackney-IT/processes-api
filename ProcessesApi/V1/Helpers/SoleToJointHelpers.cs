using System;
using System.Collections.Generic;
using System.Linq;
using ProcessesApi.V1.Domain;
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

        public static void ValidateAppointmentDateTime(Stateless.StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;

            ValidateFormData(processRequest.FormData, new List<string>() { SoleToJointFormDataKeys.AppointmentDateTime });
            var appointmentDetails = processRequest.FormData[SoleToJointFormDataKeys.AppointmentDateTime];

            if (!DateTime.TryParse(appointmentDetails.ToString(), out DateTime appointmentDateTime))
                throw new FormDataFormatException("appointment datetime", appointmentDetails);
        }
    }
}
