using Hackney.Core.JWT;
using ProcessesApi.V1.Domain;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface IUpdateProcessByIdUsecase
    {
        Task<Process> Execute(Guid id, Dictionary<string, object> formData, List<Guid> documents, Assignment assignment, string processName, int? ifMatch, Token token);
    }    
}

