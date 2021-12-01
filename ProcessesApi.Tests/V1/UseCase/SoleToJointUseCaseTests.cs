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

            _mockSTJService.Setup(x => x.Process(It.IsAny<SoleToJointObject<SoleToJointTriggers>>(), It.IsAny<SoleToJointProcess>()));

            var response = await _classUnderTest.Execute(
                processId, SoleToJointTriggers.StartApplication,
                createProcessQuery.TargetId, createProcessQuery.RelatedEntities, createProcessQuery.FormData,
                createProcessQuery.Documents, processName).ConfigureAwait(false);
            _mockSTJService.Verify(x => x.Process(It.IsAny<SoleToJointObject<SoleToJointTriggers>>(), It.IsAny<SoleToJointProcess>()), Times.Once);
            _mockGateway.Verify(x => x.SaveProcess(It.IsAny<SoleToJointProcess>()), Times.Once);

            response.PreviousStates.Should().BeEmpty();
            response.Id.Should().Be(processId);
            response.TargetId.Should().Be(createProcessQuery.TargetId);
            response.ProcessName.Should().Be(processName);
            response.RelatedEntities.Should().BeEquivalentTo(createProcessQuery.RelatedEntities);
            response.CurrentState.State.Should().Be(SoleToJointStates.SelectTenants.ToString());
            response.CurrentState.PermittedTriggers.Should().BeEquivalentTo(new List<string>() { SoleToJointPermittedTriggers.CheckEligibility.ToString() });
            response.CurrentState.ProcessData.FormData.Should().Be(createProcessQuery.FormData);
            response.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(createProcessQuery.Documents);
            response.CurrentState.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            response.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            // response.Should().BeEquivalentTo(createProcessQuery.ToResponse(),config => config.Excluding(x => x.PreviousStates));//.Excluding(x => x.CurrentState));
        }

        [Fact]
        public async Task UpdateProcessSendNewStateToGateway()
        {
            var process = _fixture.Build<SoleToJointProcess>()
                                  .With(x => x.VersionNumber, (int?) null)
                                  .With(x => x.ProcessName, ProcessNamesConstants.SoleToJoint)
                                  .Create();
            var createProcessQuery = _fixture.Build<CreateProcess>()
                                             .Create();

            _mockGateway.Setup(x => x.SaveProcess(It.IsAny<SoleToJointProcess>())).ReturnsAsync(process);

            var response = await _classUnderTest.Execute(
                process.Id, SoleToJointTriggers.StartApplication,
                process.TargetId, process.RelatedEntities, createProcessQuery.FormData,
                createProcessQuery.Documents, process.ProcessName).ConfigureAwait(false);
            _mockSTJService.Verify(x => x.Process(It.IsAny<SoleToJointObject<SoleToJointTriggers>>(), It.IsAny<SoleToJointProcess>()), Times.Once);
            _mockGateway.Verify(x => x.SaveProcess(It.IsAny<SoleToJointProcess>()), Times.Once);

            response.PreviousStates.Should().BeEmpty();
            response.Should().BeEquivalentTo(process.ToResponse(), config => config.Excluding(x => x.PreviousStates));//.Excluding(x => x.CurrentState));
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
    }
}
