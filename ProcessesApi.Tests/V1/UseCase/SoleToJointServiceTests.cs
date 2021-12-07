using AutoFixture;
using FluentAssertions;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.UseCase;
using System;
using System.Collections.Generic;
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
        public async Task InitialiseStateToSelectTenantsIfCurrentStateIsNotDefined()
        {
            // Arrange
            var processData = _fixture.Create<Process>(); // set up some mock data
            var process = Process.Create(processData.Id, new List<ProcessState>(), null, processData.TargetId, processData.RelatedEntities, ProcessNamesConstants.SoleToJoint, null);
            var triggerObject = UpdateProcessState.Create(process.Id, process.TargetId, SoleToJointTriggers.StartApplication, processData.CurrentState.ProcessData.FormData, processData.CurrentState.ProcessData.Documents, process.RelatedEntities);
            // Act
            await _classUnderTest.Process(triggerObject, process).ConfigureAwait(false);
            // Assert
            process.PreviousStates.Should().BeEmpty();
            process.CurrentState.State.Should().Be(SoleToJointStates.SelectTenants);
            process.CurrentState.PermittedTriggers.Should().BeEquivalentTo(new List<string>() { SoleToJointPermittedTriggers.CheckEligibility });
            process.CurrentState.ProcessData.FormData.Should().Be(processData.CurrentState.ProcessData.FormData);
            process.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(processData.CurrentState.ProcessData.Documents);
            process.CurrentState.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            process.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
        }
    }
}
