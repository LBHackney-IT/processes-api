using Hackney.Core.JWT;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface IUpdateProcessByIdUsecase
    {
        Task<Process> Execute(ProcessQuery query, UpdateProcessByIdRequestObject requestObject, int? ifMatch, Token token);
    }
}

