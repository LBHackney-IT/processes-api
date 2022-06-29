using System;
using System.Threading.Tasks;
using Hackney.Shared.Person.Boundary.Response;
using Hackney.Shared.Tenure.Boundary.Requests;

namespace ProcessesApi.V1.Gateways
{
    public interface ITenureApiGateway
    {
        Task EditTenureDetailsById(Guid id, EditTenureDetailsRequestObject updateTenureRequestObject, int? ifMatch);
        Task<TenureResponseObject> CreateNewTenure(CreateTenureRequestObject createTenureRequestObject);
        Task UpdateTenureForPerson(Guid tenureId, Guid personId, UpdateTenureForPersonRequestObject requestObject, int? ifMatch);
    }
}
