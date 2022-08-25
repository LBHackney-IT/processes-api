using System;
using System.Collections.Generic;
using ProcessesApi.V1.Services.Exceptions;

namespace ProcessesApi.V2.Services.Exceptions
{
    public class FormDataValueInvalidException : FormDataInvalidException
    {
        public List<string> ExpectedFormDataValues { get; private set; }
        public string IncomingFormDataValue { get; private set; }

        public FormDataValueInvalidException(string keyName, string incoming, List<string> expected)
            : base(string.Format("The form data value supplied for key {0} does not match any of the expected values ({1}). The value supplied was: {2}",
                                 keyName,
                                 String.Join(", ", expected),
                                 incoming))
        {

            IncomingFormDataValue = incoming;
            ExpectedFormDataValues = expected;
        }
    }
}
