using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Infrastructure;
using ProcessesApi.V1.Boundary.Request;
using System;
using System.Collections.Generic;

namespace ProcessesApi.V1.Factories
{
    public static class CreateRequestFactory
    {
        public static ProcessesDb ToDatabase(this CreateProcessQuery createProcessQuery)
        {
            if (createProcessQuery == null) return null;
            return new ProcessesDb
            {
                Id = createProcessQuery.Id == Guid.Empty ? Guid.NewGuid() : createProcessQuery.Id,
                TargetId = createProcessQuery.TargetId,
                RelatedEntities = createProcessQuery.RelatedEntities,
                CurrentState = new ProcessState
                {
                    StateName = null,
                    PermittedTriggers = new List<string>(),
                    Assignment = new Assignment(),
                    ProcessData = new ProcessRequest
                    {
                        FormData = createProcessQuery.FormData,
                        Documents = createProcessQuery.Documents
                    },
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow
                },
                PreviousStates = new List<ProcessState>()
                // Will change once the logic is implemented
            };
        }
    }
}