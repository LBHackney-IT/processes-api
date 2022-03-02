using AutoFixture;
using FluentAssertions;
using Hackney.Core.JWT;
using Hackney.Core.Sns;
using Moq;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase;
using ProcessesApi.V1.UseCase.Exceptions;
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
        private readonly Mock<ISnsGateway> _processesSnsGateway;
        private readonly ProcessesSnsFactory _processesSnsFactory;
        public SoleToJointUseCaseTests()
        {
            _mockGateway = new Mock<IProcessesGateway>();
            _mockSTJService = new Mock<ISoleToJointService>();
            _processesSnsGateway = new Mock<ISnsGateway>();
            _processesSnsFactory = new ProcessesSnsFactory();
            _classUnderTest = new SoleToJointUseCase(_mockGateway.Object, _mockSTJService.Object,
                                                     _processesSnsGateway.Object, _processesSnsFactory);
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
            var token = new Token();
            // Act
            var response = await _classUnderTest.Execute(
                processId, SoleToJointInternalTriggers.StartApplication,
                createProcessQuery.TargetId, createProcessQuery.RelatedEntities, createProcessQuery.FormData,
                createProcessQuery.Documents, processName, null, token).ConfigureAwait(false);
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
            //Arrange
            var createProcessQuery = _fixture.Create<CreateProcess>();
            var process = Process.Create(Guid.NewGuid(), new List<ProcessState>(), null, createProcessQuery.TargetId, createProcessQuery.RelatedEntities, ProcessNamesConstants.SoleToJoint, null);
            var token = new Token();
            var exception = new ApplicationException("Test Exception");
            _mockGateway.Setup(x => x.SaveProcess(It.IsAny<Process>())).ThrowsAsync(exception);

            //Act
            Func<Task<Process>> func = async () => await _classUnderTest.Execute(
                process.Id, SoleToJointInternalTriggers.StartApplication,
                process.TargetId, process.RelatedEntities, createProcessQuery.FormData,
                createProcessQuery.Documents, process.ProcessName, null, token).ConfigureAwait(false);

            //Assert
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }

        [Fact]
        public async Task UpdateProcessCallsServiceAndSendsNewStateToGateway()
        {
            // Arrange
            var process = CreateProcessInInitialState();
            var updateProcessQuery = _fixture.Create<UpdateProcessQueryObject>();
            _mockGateway.Setup(x => x.GetProcessById(process.Id)).ReturnsAsync(process);
            var token = new Token();

            // Act
            var response = await _classUnderTest.Execute(
                process.Id, SoleToJointPermittedTriggers.CheckEligibility,
                process.TargetId, process.RelatedEntities, updateProcessQuery.FormData,
                updateProcessQuery.Documents, process.ProcessName, 0, token).ConfigureAwait(false);

            // Assert
            _mockSTJService.Verify(x => x.Process(It.IsAny<UpdateProcessState>(), It.IsAny<Process>()), Times.Once);
            _mockGateway.Verify(x => x.SaveProcess(It.IsAny<Process>()), Times.Once);

            response.Id.Should().Be(process.Id);
            response.TargetId.Should().Be(process.TargetId);
            response.ProcessName.Should().Be(process.ProcessName);
            response.RelatedEntities.Should().BeEquivalentTo(process.RelatedEntities);
        }

        [Fact]
        public void UpdateProcessThrowsErrorOnVersionConflict()
        {
            // Arrange
            var process = CreateProcessInInitialState();
            var updateProcessQuery = _fixture.Create<UpdateProcessQueryObject>();
            _mockGateway.Setup(x => x.GetProcessById(process.Id)).ReturnsAsync(process);
            var suppliedVersion = 1;
            var token = new Token();

            // Act
            Func<Task<Process>> func = async () => await _classUnderTest.Execute(
                process.Id, SoleToJointPermittedTriggers.CheckEligibility,
                process.TargetId, process.RelatedEntities, updateProcessQuery.FormData,
                updateProcessQuery.Documents, process.ProcessName, suppliedVersion, token).ConfigureAwait(false);

            //Assert
            func.Should().Throw<VersionNumberConflictException>().WithMessage($"The version number supplied ({suppliedVersion}) does not match the current value on the entity ({0}).");
        }

        [Fact]
        public void UpdateProcessExceptionIsThrown()
        {
            //Arrange
            var process = CreateProcessInInitialState();
            var updateProcessQuery = _fixture.Create<UpdateProcessQueryObject>();
            var token = new Token();
            var exception = new ApplicationException("Test Exception");
            _mockGateway.Setup(x => x.GetProcessById(It.IsAny<Guid>())).ThrowsAsync(exception);

            //Act
            Func<Task<Process>> func = async () => await _classUnderTest.Execute(
                process.Id, SoleToJointPermittedTriggers.CheckEligibility,
                process.TargetId, process.RelatedEntities, updateProcessQuery.FormData,
                updateProcessQuery.Documents, process.ProcessName, 0, token).ConfigureAwait(false);

            //Assert
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }
    }
}
