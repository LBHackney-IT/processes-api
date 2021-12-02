using AutoFixture;
using FluentAssertions;
using Moq;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.Enums;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.UseCase;
using ProcessesApi.V1.UseCase.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Xunit;

namespace ProcessesApi.Tests.V1.UseCase
{
    public class SoleToJointServiceTests
    {
        public SoleToJointService _classUnderTest;
        public Fixture _fixture = new Fixture();

        public SoleToJointServiceTests()
        {
            _classUnderTest = new SoleToJointService();
        }

        [Fact]
        // [InlineData(SoleToJointTriggers.StartApplication, SoleToJointStates.InitialiseProcess, SoleToJointStates.SelectTenants, SoleToJointPermittedTriggers.CheckEligibility )]
        public async Task InitialiseStateToSelectTenantsIfCurrentStateIsNotDefined()
        {
            var processData = _fixture.Create<SoleToJointProcess>(); // set up some mock data
            var process = SoleToJointProcess.Create(processData.Id, new List<ProcessState<SoleToJointStates, SoleToJointTriggers>>(), null, processData.TargetId, processData.RelatedEntities, ProcessNamesConstants.SoleToJoint, null);
            var triggerObject = SoleToJointTrigger<SoleToJointTriggers>.Create(process.Id, process.TargetId, SoleToJointTriggers.StartApplication, processData.CurrentState.ProcessData.FormData, processData.CurrentState.ProcessData.Documents, process.RelatedEntities);
            // arrange
            await _classUnderTest.Process(triggerObject, process).ConfigureAwait(false);
            // act
            process.CurrentState.CurrentStateEnum.Should().Be(SoleToJointStates.SelectTenants);
            process.PreviousStates.Should().BeEmpty();
            // assert
        }
    }
}
