using Hackney.Shared.Processes.Domain;
using System;
using System.Collections.Generic;
using System.Linq;

namespace ProcessesApi.V1.Services.Exceptions
{

    public class InvalidRelatedEntitiesException : Exception
    {
        public InvalidRelatedEntitiesException() : base("Related Entities does not contain expected data.")
        {
        }

        private static string ConstructTargetAndSubTypeObject(TargetType targetType, SubType? subType)
        {
            return $"{{targetType: {targetType}, subType: {subType ?? null}}}";
        }

        public InvalidRelatedEntitiesException(TargetType targetType, SubType subType, List<RelatedEntity> relatedEntities)
            : base(String.Format("Expected Related Entities to contain {0}. Instead it contains: [{1}].",
                                 ConstructTargetAndSubTypeObject(targetType, subType),
                                 String.Join(",", relatedEntities.Select(x => ConstructTargetAndSubTypeObject(x.TargetType, x.SubType)))))
        {
        }
    }

}
