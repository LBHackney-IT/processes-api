using AutoFixture;
using FluentAssertions;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using System;
using System.Collections.Generic;
using Xunit;

namespace ProcessesApi.Tests.V1.Factories
{
    public class EntityFactoryTest
    {
        private readonly Fixture _fixture = new Fixture();

        [Fact]
        public void CanMapADatabaseEntityToADomainObject()
        {

            var entity = Process.Create(Guid.NewGuid(), new List<ProcessState>(), null, Guid.NewGuid(), new List<Guid>(), null, null);
            var databaseEntity = entity.ToDatabase();
            var domain = databaseEntity.ToDomain();

            domain.Id.Should().Be(entity.Id);
            domain.TargetId.Should().Be(entity.TargetId);
            domain.RelatedEntities.Should().BeEquivalentTo(entity.RelatedEntities);
            domain.ProcessName.Should().Be(entity.ProcessName);
            domain.CurrentState.Should().BeEquivalentTo(entity.CurrentState);
            domain.PreviousStates.Should().BeEquivalentTo(entity.PreviousStates);
        }

        [Fact]
        public void CanMapADomainEntityToADatabaseObject()
        {
            var entity = Process.Create(Guid.NewGuid(), new List<ProcessState>(), null, Guid.NewGuid(), new List<Guid>(), null, null);
            var databaseEntity = entity.ToDatabase();

            databaseEntity.Id.Should().Be(entity.Id.ToString());
            databaseEntity.TargetId.Should().Be(entity.TargetId.ToString());
            databaseEntity.RelatedEntities.Should().BeEquivalentTo(entity.RelatedEntities);
            databaseEntity.ProcessName.Should().Be(entity.ProcessName);
            databaseEntity.CurrentState.Should().BeEquivalentTo(entity.CurrentState);
            databaseEntity.PreviousStates.Should().BeEquivalentTo(entity.PreviousStates);
        }
    }
}
