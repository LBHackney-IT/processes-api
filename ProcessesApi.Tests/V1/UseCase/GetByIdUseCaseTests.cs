using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase;
using Moq;
using Xunit;

namespace ProcessesApi.Tests.V1.UseCase
{
    [Collection("LogCall collection")]
    public class GetByIdUseCaseTests
    {
        private Mock<IExampleDynamoGateway> _mockGateway;
        private GetByIdUseCase _classUnderTest;

        public GetByIdUseCaseTests()
        {
            _mockGateway = new Mock<IExampleDynamoGateway>();
            _classUnderTest = new GetByIdUseCase(_mockGateway.Object);
        }

        //TODO: test to check that the use case retrieves the correct record from the database.
        //Guidance on unit testing and example of mocking can be found here https://github.com/LBHackney-IT/lbh-processes-api/wiki/Writing-Unit-Tests
    }
}
