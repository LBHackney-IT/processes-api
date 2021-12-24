using AutoFixture;
using FluentAssertions;
using Moq;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase;
using ProcessesApi.V1.UseCase.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Xunit;

namespace ProcessesApi.Tests.V1.UseCase
{
    [Collection("LogCall collection")]
    public class ProcessUseCaseTests
    {
        private Mock<IProcessesGateway> _mockGateway;
        private ProcessUseCase _classUnderTest;
        private Mock<IProcessService> _mockProcessService;
        private readonly Fixture _fixture = new Fixture();
        public ProcessUseCaseTests()
        {
            _mockGateway = new Mock<IProcessesGateway>();
            _mockProcessService = new Mock<IProcessService>();
            Func<string, IProcessService> _mockProcessesDelegate = (processName) => { return _mockProcessService.Object; };
            _classUnderTest = new ProcessUseCase(_mockGateway.Object, _mockProcessesDelegate);
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
        public async Task CreateNewProcessCallsServiceAndGateway()
        {
            // Arrange
            var createProcessQuery = _fixture.Create<CreateProcess>();
            var processName = ProcessNamesConstants.SoleToJoint;
            var processId = Guid.NewGuid();
            // Act
            var response = await _classUnderTest.Execute(
                processId, ProcessInternalTriggers.StartApplication,
                createProcessQuery.TargetId, createProcessQuery.RelatedEntities, createProcessQuery.FormData,
                createProcessQuery.Documents, processName, null).ConfigureAwait(false);
            // Assert
            _mockProcessService.Verify(x => x.Process(It.IsAny<UpdateProcessState>(), It.IsAny<Process>()), Times.Once);
            _mockGateway.Verify(x => x.SaveProcess(It.IsAny<Process>()), Times.Once);

            response.Id.Should().Be(processId);
            response.TargetId.Should().Be(createProcessQuery.TargetId);
            response.ProcessName.Should().Be(processName);
            response.RelatedEntities.Should().BeEquivalentTo(createProcessQuery.RelatedEntities);
        }

        [Fact]
        public void CreateNewProcessExceptionIsThrown()
        {
            var createProcessQuery = _fixture.Create<CreateProcess>();

            var exception = new ApplicationException("Test Exception");
            _mockGateway.Setup(x => x.SaveProcess(It.IsAny<Process>())).ThrowsAsync(exception);

            Func<Task<Process>> func = async () => await _classUnderTest.Execute(
                Guid.NewGuid(), ProcessInternalTriggers.StartApplication,
                createProcessQuery.TargetId, createProcessQuery.RelatedEntities, createProcessQuery.FormData,
                createProcessQuery.Documents, ProcessNamesConstants.SoleToJoint, null).ConfigureAwait(false);
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }

        [Fact]
        public async Task UpdateProcessCallsServiceAndSendsNewStateToGateway()
        {
            // Arrange
            var process = CreateProcessInInitialState();
            var updateProcessQuery = _fixture.Create<UpdateProcessQueryObject>();
            _mockGateway.Setup(x => x.GetProcessById(process.Id)).ReturnsAsync(process);
            // Act
            var response = await _classUnderTest.Execute(
                process.Id, SoleToJointPermittedTriggers.CheckEligibility,
                process.TargetId, process.RelatedEntities, updateProcessQuery.FormData,
                updateProcessQuery.Documents, process.ProcessName, 0).ConfigureAwait(false);

            // Assert
            _mockProcessService.Verify(x => x.Process(It.IsAny<UpdateProcessState>(), It.IsAny<Process>()), Times.Once);
            _mockGateway.Verify(x => x.SaveProcess(It.IsAny<Process>()), Times.Once);

            response.Id.Should().Be(process.Id);
            response.TargetId.Should().Be(process.TargetId);
            response.ProcessName.Should().Be(process.ProcessName);
            response.RelatedEntities.Should().BeEquivalentTo(process.RelatedEntities);
        }

        [Fact]
        public void UpdateProcessExceptionIsThrown()
        {
            var process = CreateProcessInInitialState();
            var updateProcessQuery = _fixture.Create<UpdateProcessQueryObject>();

            var exception = new ApplicationException("Test Exception");
            _mockGateway.Setup(x => x.GetProcessById(It.IsAny<Guid>())).ThrowsAsync(exception);

            Func<Task<Process>> func = async () => await _classUnderTest.Execute(
                process.Id, SoleToJointPermittedTriggers.CheckEligibility,
                process.TargetId, process.RelatedEntities, updateProcessQuery.FormData,
                updateProcessQuery.Documents, process.ProcessName, 0).ConfigureAwait(false);
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }
    }
}
