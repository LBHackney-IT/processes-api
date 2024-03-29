using AutoFixture;
using FluentAssertions;
using Moq;
using Hackney.Shared.Processes.Boundary.Request;
using Hackney.Shared.Processes.Domain;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase;
using System;
using System.Threading.Tasks;
using Xunit;

namespace ProcessesApi.Tests.V1.UseCase
{
    [Collection("LogCall collection")]
    public class GetByIdUseCaseTests
    {
        private Mock<IProcessesGateway> _mockGateway;
        private GetProcessByIdUseCase _classUnderTest;
        private readonly Fixture _fixture = new Fixture();
        public GetByIdUseCaseTests()
        {
            _mockGateway = new Mock<IProcessesGateway>();
            _classUnderTest = new GetProcessByIdUseCase(_mockGateway.Object);
        }

        private static ProcessQuery ConstructQuery(Guid id)
        {
            return new ProcessQuery
            {
                Id = id
            };
        }

        [Fact]
        public async Task GetProcessByIdReturnsNullIfNullResponseFromGateway()
        {
            var id = Guid.NewGuid();
            _mockGateway.Setup(x => x.GetProcessById(id)).ReturnsAsync((Process) null);
            var query = ConstructQuery(id);

            var response = await _classUnderTest.Execute(query).ConfigureAwait(false);
            response.Should().BeNull();
        }

        [Fact]
        public async Task GetProcessByIdReturnsProcessFromGateway()
        {
            var process = _fixture.Create<Process>();
            var query = ConstructQuery(process.Id);

            _mockGateway.Setup(x => x.GetProcessById(process.Id)).ReturnsAsync((Process) process);

            var response = await _classUnderTest.Execute(query).ConfigureAwait(false);
            response.Should().BeEquivalentTo(process);
        }

        [Fact]
        public async Task GetProcessByIdExceptionIsThrown()
        {
            var id = Guid.NewGuid();
            var exception = new ApplicationException("Test Exception");
            _mockGateway.Setup(x => x.GetProcessById(id)).ThrowsAsync(exception);
            var query = ConstructQuery(id);

            Func<Task<Process>> func = async () => await _classUnderTest.Execute(query).ConfigureAwait(false);
            (await func.Should().ThrowAsync<ApplicationException>()).WithMessage(exception.Message);
        }
    }
}
