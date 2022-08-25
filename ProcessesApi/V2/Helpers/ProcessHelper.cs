using ProcessesApi.V1.Constants;
using ProcessesApi.V2.Domain;
using ProcessesApi.V2.Services.Exceptions;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcessesApi.V2.Helpers
{
    public static class ProcessHelper
    {
        public static void ValidateKeys(this Dictionary<string, object> requestFormData, List<string> expectedFormDataKeys)
        {
            expectedFormDataKeys.ForEach(x =>
            {
                if (!requestFormData.ContainsKey(x))
                    throw new FormDataNotFoundException(requestFormData.Keys.ToList(), expectedFormDataKeys);
            });
        }

        public static void ValidateOptionalKeys(this Dictionary<string, object> requestFormData, List<string> expectedFormDataKeys)
        {
            if (!expectedFormDataKeys.Any(x => requestFormData.ContainsKey(x)))
                throw new FormDataNotFoundException(requestFormData.Keys.ToList(), expectedFormDataKeys);

        }

        public static Dictionary<string, object> CreateEventData(this Dictionary<string, object> requestFormData, List<string> selectedKeys)
        {
            return requestFormData.Where(x => selectedKeys.Contains(x.Key))
                                  .ToDictionary(val => val.Key, val => val.Value);
        }

        public static Dictionary<string, object> ValidateHasNotifiedResident(this ProcessTrigger processRequest)
        {
            var formData = processRequest.FormData;
            ProcessHelper.ValidateKeys(formData, new List<string>() { SharedKeys.HasNotifiedResident });

            var eventData = new Dictionary<string, object>();

            if (formData.ContainsKey(SharedKeys.Reason))
                eventData = ProcessHelper.CreateEventData(formData, new List<string> { SharedKeys.Reason });

            var hasNotifiedResidentString = processRequest.FormData[SharedKeys.HasNotifiedResident];

            if (Boolean.TryParse(hasNotifiedResidentString.ToString(), out bool hasNotifiedResident))
            {
                if (!hasNotifiedResident)
                    throw new FormDataInvalidException("Housing Officer must notify the resident before closing this process.");
                return eventData;
            }
            else
            {
                throw new FormDataFormatException(typeof(bool), hasNotifiedResidentString);
            }
        }


    }
}
