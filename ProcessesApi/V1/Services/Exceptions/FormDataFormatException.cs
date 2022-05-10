namespace ProcessesApi.V1.Services.Exceptions
{
    public class FormDataFormatException : FormDataInvalidException
    {
        public FormDataFormatException() : base("One of the form data values is not in the correct format.")
        {
        }

        public FormDataFormatException(string valueType, object value)
            : base($"The {valueType} provided ({value.ToString()}) is not in the correct format.")
        {
        }
    }
}
