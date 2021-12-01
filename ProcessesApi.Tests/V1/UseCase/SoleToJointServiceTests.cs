using AutoFixture;
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
        }

        [Fact]
       // [InlineData(SoleToJointTriggers.StartApplication, SoleToJointStates.ApplicationStarted, SoleToJointStates.SelectTenants, SoleToJointPermittedTriggers.CheckEligibility )]
        public async Task StartSoleToJointState()
        {
            var id = Guid.NewGuid();
            var processTrigger = _fixture.Build<SoleToJointTrigger<SoleToJointTriggers>>()
                                          .With(x => x.Id, id)
                                          .Create();
            var processName = ProcessNamesConstants.SoleToJoint;
            var currentState = ProcessState<SoleToJointStates, SoleToJointTriggers>.Create(SoleToJointStates.ApplicationStarted, SoleToJointPermittedTriggers.CheckEligibility, null, null, DateTime.UtcNow, DateTime.UtcNow);
            
            var process = SoleToJointProcess.Create(id, new List<ProcessState<SoleToJointStates, SoleToJointTriggers>>(), currentState, processTrigger.TargetId, processTrigger.RelatedEntities, processName, null);
        }
    }
}
