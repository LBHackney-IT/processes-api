using AutoFixture;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure;
using FluentAssertions;
using System;
using Xunit;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Domain.Enums;
using System.Linq;

namespace ProcessesApi.Tests.V1.Factories
{
    public class EntityFactoryTest
    {
        private readonly Fixture _fixture = new Fixture();

        [Fact]
        public void CanMapADatabaseEntityToADomainObject()
        {
            //var databaseEntity = _fixture.Build<ProcessesDb>()
            //    .With(process => process.Id, Guid.NewGuid())
            //    .With(process => process.TargetId, Guid.NewGuid())
            //    .Create();
            var entity = _fixture.Create<SoleToJointProcess>();
            var databaseEntity = entity.ToDatabase();
            var domain = databaseEntity.ToDomain();

            domain.Id.Should().Be(entity.Id);
            domain.TargetId.Should().Be(entity.TargetId);
            domain.RelatedEntities.Should().BeEquivalentTo(entity.RelatedEntities);
            domain.ProcessName.Should().Be(entity.ProcessName);
            domain.CurrentState.Should().BeEquivalentTo(entity.CurrentState.ConvertStringToEnum<SoleToJointStates, SoleToJointTriggers>());
            domain.PreviousStates.Should().BeEquivalentTo(entity.PreviousStates.Select(x => x.ConvertStringToEnum<SoleToJointStates, SoleToJointTriggers>()).ToList());
        }

        [Fact]
        public void CanMapADomainEntityToADatabaseObject()
        {
            var entity = _fixture.Create<SoleToJointProcess>();
            var databaseEntity = entity.ToDatabase();

            databaseEntity.Id.Should().Be(entity.Id.ToString());
            databaseEntity.TargetId.Should().Be(entity.TargetId.ToString());
            databaseEntity.RelatedEntities.Should().BeEquivalentTo(entity.RelatedEntities);
            databaseEntity.ProcessName.Should().Be(entity.ProcessName);
            databaseEntity.CurrentState.Should().BeEquivalentTo(entity.CurrentState.ConvertEnumsToString());
            databaseEntity.PreviousStates.Should().BeEquivalentTo(entity.PreviousStates.Select(x => x.ConvertEnumsToString()));
        }
    }
}
