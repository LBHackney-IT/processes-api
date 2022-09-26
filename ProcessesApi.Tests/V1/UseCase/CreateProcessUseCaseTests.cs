using AutoFixture;
using FluentAssertions;
using Hackney.Core.JWT;
using Moq;
using Hackney.Shared.Processes.Boundary.Request;
using Hackney.Shared.Processes.Domain;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Services.Interfaces;
using System;
using System.Threading.Tasks;
using Xunit;
using ProcessesApi.V1.UseCase;

namespace ProcessesApi.Tests.V1.UseCase
{
    [Collection("LogCall collection")]
    public class CreateProcessUseCaseTests
    {
        private Mock<IProcessesGateway> _mockGateway;
        private CreateProcessUseCase _classUnderTest;
        private Mock<IProcessService> _mockProcessService;
        private readonly Fixture _fixture = new Fixture();

        public CreateProcessUseCaseTests()
        {
            _mockGateway = new Mock<IProcessesGateway>();
            _mockProcessService = new Mock<IProcessService>();
            Func<ProcessName, IProcessService> _mockProcessServiceProvider = (processName) => { return _mockProcessService.Object; };

            _classUnderTest = new CreateProcessUseCase(_mockGateway.Object, _mockProcessServiceProvider);
        }

        [Fact]
        public async Task CreateNewProcessCallsServiceAndGateway()
        {
            // Arrange
            var request = _fixture.Create<CreateProcess>();
            var processName = _fixture.Create<ProcessName>();
            var processId = Guid.NewGuid();
            var token = new Token();
            // Act
            var response = await _classUnderTest.Execute(request, processName, token).ConfigureAwait(false);
            // Assert
            _mockProcessService.Verify(x => x.Process(It.IsAny<ProcessTrigger>(), It.IsAny<Process>(), token), Times.Once);
            _mockGateway.Verify(x => x.SaveProcess(It.IsAny<Process>()), Times.Once);

            response.TargetId.Should().Be(request.TargetId);
            response.ProcessName.Should().Be(processName);
            response.RelatedEntities.Should().BeEquivalentTo(request.RelatedEntities);
        }

        [Fact]
        public void CreateNewProcessExceptionIsThrown()
        {
            //Arrange
            var request = _fixture.Create<CreateProcess>();
            var process = _fixture.Create<Process>();
            var token = new Token();
            var exception = new ApplicationException("Test Exception");
            _mockGateway.Setup(x => x.SaveProcess(It.IsAny<Process>())).ThrowsAsync(exception);

            //Act
            Func<Task<Process>> func = async () => await _classUnderTest.Execute(request, process.ProcessName, token).ConfigureAwait(false);

            //Assert
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }
    }
}
