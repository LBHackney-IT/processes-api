using System;

namespace ProcessesApi.V1.Services.Exceptions
{
    public class FormDataInvalidException : Exception
    {
        public FormDataInvalidException() : base("The request's FormData is invalid.")
        {
        }

        public FormDataInvalidException(string message) : base($"The request's FormData is invalid: {message}")
        {
        }
    }
}
