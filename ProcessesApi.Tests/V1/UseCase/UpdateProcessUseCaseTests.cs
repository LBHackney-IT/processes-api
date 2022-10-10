using AutoFixture;
using FluentAssertions;
using Hackney.Core.JWT;
using Moq;
using Hackney.Shared.Processes.Boundary.Request;
using Hackney.Shared.Processes.Domain;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.Services.Interfaces;
using ProcessesApi.V1.UseCase.Exceptions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;
using ProcessesApi.V1.UseCase;

namespace ProcessesApi.Tests.V1.UseCase
{
    [Collection("LogCall collection")]
    public class UpdateProcessUseCaseTests
    {
        private Mock<IProcessesGateway> _mockGateway;
        private UpdateProcessUseCase _classUnderTest;
        private Mock<IProcessService> _mockProcessService;
        private readonly Fixture _fixture = new Fixture();

        public UpdateProcessUseCaseTests()
        {
            _mockGateway = new Mock<IProcessesGateway>();
            _mockProcessService = new Mock<IProcessService>();
            Func<ProcessName, IProcessService> _mockProcessServiceProvider = (processName) => { return _mockProcessService.Object; };

            _classUnderTest = new UpdateProcessUseCase(_mockGateway.Object, _mockProcessServiceProvider);
        }

        private Process CreateProcessInInitialState()
        {
            return _fixture.Build<Process>()
                    .With(x => x.CurrentState, (ProcessState) null)
                    .With(x => x.PreviousStates, new List<ProcessState>())
                    .With(x => x.VersionNumber, 0)
                    .Create();
        }

        [Fact]
        public async Task UpdateProcessCallsServiceAndSendsNewStateToGateway()
        {
            // Arrange
            var process = CreateProcessInInitialState();
            var request = _fixture.Build<UpdateProcessQuery>().With(x => x.Id, process.Id).Create();
            var requestObject = _fixture.Create<UpdateProcessRequestObject>();
            var token = new Token();

            _mockGateway.Setup(x => x.GetProcessById(process.Id)).ReturnsAsync(process);

            // Act
            var response = await _classUnderTest.Execute(request, requestObject, 0, token).ConfigureAwait(false);

            // Assert
            _mockProcessService.Verify(x => x.Process(It.IsAny<ProcessTrigger>(), It.IsAny<Process>(), token), Times.Once);
            _mockGateway.Verify(x => x.SaveProcess(It.IsAny<Process>()), Times.Once);

            response.Should().BeEquivalentTo(process);
        }

        [Fact]
        public async Task UpdateProcessThrowsErrorOnVersionConflict()
        {
            // Arrange
            var process = CreateProcessInInitialState();
            var request = _fixture.Build<UpdateProcessQuery>().With(x => x.Id, process.Id).Create();
            var requestObject = _fixture.Create<UpdateProcessRequestObject>();
            var suppliedVersion = 1;
            var token = new Token();

            _mockGateway.Setup(x => x.GetProcessById(process.Id)).ReturnsAsync(process);

            // Act + Assert
            (await _classUnderTest.Invoking(x => x.Execute(request, requestObject, suppliedVersion, token))
                           .Should()
                           .ThrowAsync<VersionNumberConflictException>())
                           .WithMessage($"The version number supplied ({suppliedVersion}) does not match the current value on the entity ({0}).");
        }

        [Fact]
        public async Task UpdateProcessExceptionIsThrown()
        {
            //Arrange
            var process = CreateProcessInInitialState();
            var request = _fixture.Create<UpdateProcessQuery>();
            var requestObject = _fixture.Create<UpdateProcessRequestObject>();
            var token = new Token();

            var exception = new ApplicationException("Test Exception");
            _mockGateway.Setup(x => x.GetProcessById(It.IsAny<Guid>())).ThrowsAsync(exception);

            //Act
            Func<Task<Process>> func = async () => await _classUnderTest.Execute(request, requestObject, 0, token).ConfigureAwait(false);

            //Assert
            (await func.Should().ThrowAsync<ApplicationException>()).WithMessage(exception.Message);
        }
    }
}
