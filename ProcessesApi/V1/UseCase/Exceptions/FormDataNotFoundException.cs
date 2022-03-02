using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcessesApi.V1.UseCase.Exceptions
{
    public class FormDataNotFoundException : Exception
    {
        public List<string> ExpectedFormDataKeys { get; private set; }
        public List<string> IncomingFormDataKeys { get; private set; }

        public FormDataNotFoundException(List<string> incoming, List<string> expected)
            : base(string.Format("The form data keys supplied ({0}) do not include the expected values ({1}).",
                                 String.Join(", ", incoming),
                                 String.Join(", ", expected.Except(incoming))))
        {
            IncomingFormDataKeys = incoming;
            ExpectedFormDataKeys = expected;
        }
    }
}
