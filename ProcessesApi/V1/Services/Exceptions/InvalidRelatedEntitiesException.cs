using Hackney.Shared.Processes.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcessesApi.V1.Services.Exceptions
{

    public class InvalidRelatedEntitiesException : FormDataInvalidException
    {
        public InvalidRelatedEntitiesException() : base("Related Entities does not contain expected data.")
        {
        }

        public InvalidRelatedEntitiesException(string expectedType, List<RelatedEntity> relatedEntities)
            : base($"Expected Related Entities to contain {expectedType}. Instead it contains:{ String.Join(",", relatedEntities)}.")
        {
        }
    }

}
