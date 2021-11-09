using ProcessesApi.V1.Boundary.Response;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface IGetAllUseCase
    {
        ResponseObjectList Execute();
    }
}
