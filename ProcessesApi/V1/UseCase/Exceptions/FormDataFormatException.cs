using System;

namespace ProcessesApi.V1.UseCase.Exceptions
{
    public class FormDataFormatException : Exception
    {

        public FormDataFormatException(string valueType, object value)
            : base($"The {valueType} provided ({value.ToString()}) is not in the correct format.")
        {
        }
    }
}
