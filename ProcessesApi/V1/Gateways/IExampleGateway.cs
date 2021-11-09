using System.Collections.Generic;
using ProcessesApi.V1.Domain;

namespace ProcessesApi.V1.Gateways
{
    public interface IExampleGateway
    {
        Entity GetEntityById(int id);

        List<Entity> GetAll();
    }
}
