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
    public class SoleToJointUseCaseTests
    {
        private Mock<IProcessesGateway> _mockGateway;
        private SoleToJointUseCase _classUnderTest;
        private Mock<ISoleToJointService> _mockSTJService;
        private readonly Fixture _fixture = new Fixture();
        public SoleToJointUseCaseTests()
        {
            _mockGateway = new Mock<IProcessesGateway>();
            _mockSTJService = new Mock<ISoleToJointService>();
            _classUnderTest = new SoleToJointUseCase(_mockGateway.Object, _mockSTJService.Object);
        }

        private Process CreateProcessInInitialState()
        {
            return _fixture.Build<Process>()
                    .With(x => x.CurrentState, (ProcessState) null)
                    .With(x => x.PreviousStates, new List<ProcessState>())
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
                processId, SoleToJointTriggers.StartApplication,
                createProcessQuery.TargetId, createProcessQuery.RelatedEntities, createProcessQuery.FormData,
                createProcessQuery.Documents, processName).ConfigureAwait(false);
            // Assert
            _mockSTJService.Verify(x => x.Process(It.IsAny<UpdateProcessState>(), It.IsAny<Process>()), Times.Once);
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
            var process = Process.Create(Guid.NewGuid(), new List<ProcessState>(), null, createProcessQuery.TargetId, createProcessQuery.RelatedEntities, ProcessNamesConstants.SoleToJoint, null);

            var exception = new ApplicationException("Test Exception");
            _mockGateway.Setup(x => x.SaveProcess(It.IsAny<Process>())).ThrowsAsync(exception);

            Func<Task<Process>> func = async () => await _classUnderTest.Execute(
                process.Id, SoleToJointTriggers.StartApplication,
                process.TargetId, process.RelatedEntities, createProcessQuery.FormData,
                createProcessQuery.Documents, process.ProcessName).ConfigureAwait(false);
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
                process.Id, SoleToJointTriggers.CheckEligibility,
                process.TargetId, process.RelatedEntities, updateProcessQuery.FormData,
                updateProcessQuery.Documents, process.ProcessName).ConfigureAwait(false);

            // Assert
            _mockSTJService.Verify(x => x.Process(It.IsAny<UpdateProcessState>(), It.IsAny<Process>()), Times.Once);
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
                process.Id, SoleToJointTriggers.CheckEligibility,
                process.TargetId, process.RelatedEntities, updateProcessQuery.FormData,
                updateProcessQuery.Documents, process.ProcessName).ConfigureAwait(false);
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }
    }
}
