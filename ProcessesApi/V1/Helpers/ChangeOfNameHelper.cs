using ProcessesApi.V1.Services.Exceptions;
using System.Collections.Generic;
using System.Linq;

namespace ProcessesApi.V1.Helpers
{
    public static class ChangeOfNameHelper
    {
        public static void ValidateOptionalFormData(Dictionary<string, object> requestFormData, List<string> expectedFormDataKeys)
        {
            if (!requestFormData.ContainsKey("firstName") && !requestFormData.ContainsKey("surname") &&
                !requestFormData.ContainsKey("middleName") && !requestFormData.ContainsKey("Title"))
                throw new FormDataNotFoundException(requestFormData.Keys.ToList(), expectedFormDataKeys);
        }
    }
}
