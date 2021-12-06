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
using ProcessesApi.V1.UseCase.Interfaces;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Domain.Enums;
using ProcessesApi.V1.Boundary.Constants;
using System.Collections.Generic;

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

        [Fact]
        public async Task CreateNewProcessReturnsProcessFromGateway()
        {

            var createProcessQuery = _fixture.Build<CreateProcess>()
                                             .Create();

            var processName = ProcessNamesConstants.SoleToJoint;
            var processId = Guid.NewGuid();

            var response = await _classUnderTest.Execute(
                processId, SoleToJointTriggers.StartApplication,
                createProcessQuery.TargetId, createProcessQuery.RelatedEntities, createProcessQuery.FormData,
                createProcessQuery.Documents, processName).ConfigureAwait(false);
            _mockSTJService.Verify(x => x.Process(It.IsAny<SoleToJointTrigger>(), It.IsAny<SoleToJointProcess>()), Times.Once);
            _mockGateway.Verify(x => x.SaveProcess(It.IsAny<SoleToJointProcess>()), Times.Once);

            response.Id.Should().Be(processId);
            response.TargetId.Should().Be(createProcessQuery.TargetId);
            response.ProcessName.Should().Be(processName);
            response.RelatedEntities.Should().BeEquivalentTo(createProcessQuery.RelatedEntities);
        }

        [Fact]
        public void CreateNewProcessExceptionIsThrown()
        {
            var process = _fixture.Build<SoleToJointProcess>()
                                 .With(x => x.VersionNumber, (int?) null)
                                 .With(x => x.ProcessName, ProcessNamesConstants.SoleToJoint)
                                 .Create();
            var createProcessQuery = _fixture.Build<CreateProcess>()
                                             .Create();

            var exception = new ApplicationException("Test Exception");
            _mockGateway.Setup(x => x.SaveProcess(It.IsAny<SoleToJointProcess>())).ThrowsAsync(exception);

            Func<Task<SoleToJointProcess>> func = async () => await _classUnderTest.Execute(
                process.Id, SoleToJointTriggers.StartApplication,
                process.TargetId, process.RelatedEntities, createProcessQuery.FormData,
                createProcessQuery.Documents, process.ProcessName).ConfigureAwait(false);
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }

        [Fact]
        public async Task UpdateProcessSendNewStateToGateway()
        {
            var process = _fixture.Build<SoleToJointProcess>()
                                  .With(x => x.VersionNumber, (int?) null)
                                  .With(x => x.ProcessName, ProcessNamesConstants.SoleToJoint)
                                  .Create();

            var updateProcessQuery = _fixture.Build<UpdateProcessQueryObject>()
                                             .Create();


            _mockGateway.Setup(x => x.GetProcessById(process.Id)).ReturnsAsync(process);

            var response = await _classUnderTest.Execute(
                process.Id, SoleToJointTriggers.CheckEligibility,
                process.TargetId, process.RelatedEntities, updateProcessQuery.FormData,
                updateProcessQuery.Documents, process.ProcessName).ConfigureAwait(false);
            _mockSTJService.Verify(x => x.Process(It.IsAny<SoleToJointTrigger>(), It.IsAny<SoleToJointProcess>()), Times.Once);
            _mockGateway.Verify(x => x.SaveProcess(It.IsAny<SoleToJointProcess>()), Times.Once);

            response.Id.Should().Be(process.Id);
            response.TargetId.Should().Be(process.TargetId);
            response.ProcessName.Should().Be(process.ProcessName);
            response.RelatedEntities.Should().BeEquivalentTo(process.RelatedEntities);
        }

        [Fact]
        public void UpdateProcessExceptionIsThrown()
        {
            var process = _fixture.Build<SoleToJointProcess>()
                                 .With(x => x.VersionNumber, (int?) null)
                                 .With(x => x.ProcessName, ProcessNamesConstants.SoleToJoint)
                                 .Create();
            var updateProcessQuery = _fixture.Build<UpdateProcessQueryObject>()
                                             .Create();

            var exception = new ApplicationException("Test Exception");
            _mockGateway.Setup(x => x.GetProcessById(It.IsAny<Guid>())).ThrowsAsync(exception);

            Func<Task<SoleToJointProcess>> func = async () => await _classUnderTest.Execute(
                process.Id, SoleToJointTriggers.CheckEligibility,
                process.TargetId, process.RelatedEntities, updateProcessQuery.FormData,
                updateProcessQuery.Documents, process.ProcessName).ConfigureAwait(false);
            func.Should().Throw<ApplicationException>().WithMessage(exception.Message);
        }
    }
}
