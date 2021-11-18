using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Boundary.Response;
using Moq;
using FluentAssertions;
using AutoFixture;
using System.Threading.Tasks;
using System;
using Xunit;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Domain;

namespace ProcessesApi.Tests.V1.UseCase
{
    [Collection("LogCall collection")]
    public class CreateNewProcessUseCaseTests
    {
        private Mock<IProcessesGateway> _mockGateway;
        private CreateNewProcessUseCase _classUnderTest;
        private readonly Fixture _fixture = new Fixture();
        public CreateNewProcessUseCaseTests()
        {
            _mockGateway = new Mock<IProcessesGateway>();
            _classUnderTest = new CreateNewProcessUseCase(_mockGateway.Object);
        }

        [Fact]
        public async Task CreateNewProcessReturnsProcessFromGateway()
        {
            var createProcessQuery = _fixture.Create<CreateProcessQuery>();
            var process = _fixture.Create<Process>();
            var processName = process.ProcessName;

            _mockGateway.Setup(x => x.CreateNewProcess(createProcessQuery, processName)).ReturnsAsync(process);

            var response = await _classUnderTest.Execute(createProcessQuery, processName).ConfigureAwait(false);
            response.Should().BeEquivalentTo(process.ToResponse());
        }

        [Fact]
        public void CreateNewProcessExceptionIsThrown()
        {
            var createProcessQuery = _fixture.Create<CreateProcessQuery>();
            var processName = "test-process";

            var exception = new ApplicationException("Test Exception");
            _mockGateway.Setup(x => x.CreateNewProcess(createProcessQuery, processName)).ThrowsAsync(exception);

            Func<Task<ProcessResponse>> func = async () => await _classUnderTest.Execute(createProcessQuery, processName).ConfigureAwait(false);
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }
    }
}
