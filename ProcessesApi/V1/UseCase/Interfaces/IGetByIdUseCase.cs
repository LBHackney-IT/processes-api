using ProcessesApi.V1.Boundary.Response;

namespace ProcessesApi.V1.UseCase.Interfaces
{
    public interface IGetByIdUseCase
    {
        ResponseObject Execute(int id);
    }
}
