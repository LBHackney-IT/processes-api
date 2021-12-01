using AutoFixture;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure;
using FluentAssertions;
using System;
using Xunit;
using ProcessesApi.V1.Domain.SoleToJoint;

namespace ProcessesApi.Tests.V1.Factories
{
    public class EntityFactoryTest
    {
        private readonly Fixture _fixture = new Fixture();

        [Fact]
        public void CanMapADatabaseEntityToADomainObject()
        {
            var databaseEntity = _fixture.Build<ProcessesDb>()
                .With(process => process.Id, Guid.NewGuid())
                .With(process => process.TargetId, Guid.NewGuid())
                .Create();
            var entity = databaseEntity.ToDomain();

            entity.Id.Should().Be(databaseEntity.Id);
            entity.TargetId.Should().Be(databaseEntity.TargetId);
            entity.RelatedEntities.Should().BeEquivalentTo(databaseEntity.RelatedEntities);
            entity.ProcessName.Should().Be(databaseEntity.ProcessName);
            entity.CurrentState.Should().Be(databaseEntity.CurrentState);
            entity.PreviousStates.Should().BeEquivalentTo(databaseEntity.PreviousStates);
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
            databaseEntity.CurrentState.Should().Be(entity.CurrentState);
            databaseEntity.PreviousStates.Should().BeEquivalentTo(entity.PreviousStates);
        }
    }
}
