using ProcessesApi.V1.Constants;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Services.Exceptions;
using System.Collections.Generic;
using System.Linq;

namespace ProcessesApi.V1.Helpers
{
    public static class ProcessHelper
    {

        public static void ValidateFormData(Dictionary<string, object> requestFormData, List<string> expectedFormDataKeys)
        {
            expectedFormDataKeys.ForEach(x =>
            {
                if (!requestFormData.ContainsKey(x))
                    throw new FormDataNotFoundException(requestFormData.Keys.ToList(), expectedFormDataKeys);
            });
        }

        public static void ValidateOptionalFormData(Dictionary<string, object> requestFormData, List<string> expectedFormDataKeys)
        {
            if (!expectedFormDataKeys.Any(x => requestFormData.ContainsKey(x)))
                throw new FormDataNotFoundException(requestFormData.Keys.ToList(), expectedFormDataKeys);

        }
        public static Dictionary<string, object> CreateEventData(Dictionary<string, object> requestFormData, List<string> selectedKeys)
        {
            return requestFormData.Where(x => selectedKeys.Contains(x.Key))
                                  .ToDictionary(val => val.Key, val => val.Value);
        }
    }
}
