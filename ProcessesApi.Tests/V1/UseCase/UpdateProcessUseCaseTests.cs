using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Boundary.Request;
using Moq;
using FluentAssertions;
using AutoFixture;
using System.Threading.Tasks;
using Xunit;
using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Factories;

namespace ProcessesApi.Tests.V1.UseCase
{
    [Collection("LogCall collection")]
    public class UpdateProcessUseCaseTests
    {
        private Mock<IProcessesGateway> _mockGateway;
        private UpdateProcessUseCase _classUnderTest;
        private readonly Fixture _fixture = new Fixture();
        public UpdateProcessUseCaseTests()
        {
            _mockGateway = new Mock<IProcessesGateway>();
            _classUnderTest = new UpdateProcessUseCase(_mockGateway.Object);
        }

        private (Process, UpdateProcessQuery, UpdateProcessQueryObject) ConstructQueries()
        {
            var originalProcess = _fixture.Build<Process>()
                                .With(x => x.VersionNumber, (int?) null)
                                .Create();
            var query = _fixture.Build<UpdateProcessQuery>()
                                .With(x => x.ProcessName, originalProcess.ProcessName)
                                .With(x => x.Id, originalProcess.Id)
                                .Create();
            var queryObject = _fixture.Create<UpdateProcessQueryObject>();

            return (originalProcess, query, queryObject);

        }

        [Fact]
        public async Task UpdateProcessReturnsNullIfProcessDoesNotExist()
        {
            // Arrange
            (var originalProcess, var query, var queryObject) = ConstructQueries();
            var ifMatch = 0;
            _mockGateway.Setup(x => x.UpdateProcess(queryObject, query, ifMatch)).ReturnsAsync((Process) null);
            // Act
            var response = await _classUnderTest.Execute(queryObject, query, ifMatch).ConfigureAwait(false);
            // Assert
            response.Should().BeNull();
        }

        [Fact]
        public async Task UpdateProcessReturnsResponseObjectWhenSuccessfullyUpdated()
        {
            // Arrange
            (var originalProcess, var query, var queryObject) = ConstructQueries();
            var ifMatch = 0;
            var mockUpdatedProcess = _fixture.Create<Process>();
            _mockGateway.Setup(x => x.UpdateProcess(queryObject, query, ifMatch)).ReturnsAsync((Process) mockUpdatedProcess);
            // Act
            var response = await _classUnderTest.Execute(queryObject, query, ifMatch).ConfigureAwait(false);
            // Assert
            response.Should().NotBeNull();
            response.Should().BeOfType(typeof(ProcessResponse));
            response.Should().BeEquivalentTo(mockUpdatedProcess.ToResponse());
        }
    }
}
