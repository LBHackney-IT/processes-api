using AutoFixture;
using FluentAssertions;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using System;
using System.Collections.Generic;
using Xunit;

namespace ProcessesApi.Tests.V1.Factories
{
    public class ResponseFactoryTest
    {
        private readonly Fixture _fixture = new Fixture();

        [Fact]
        public void CanMapADatabaseEntityToADomainObject()
        {
            var domain = Process.Create(Guid.NewGuid(), new List<ProcessState>(), null, Guid.NewGuid(), TargetType.tenure,new List<RelatedEntities>(), ProcessName.soletojoint, null);
            var response = domain.ToResponse();

            response.Id.Should().Be(domain.Id);
            response.TargetId.Should().Be(domain.TargetId);
            response.TargetType.Should().Be(domain.TargetType);
            response.RelatedEntities.Should().BeEquivalentTo(domain.RelatedEntities);
            response.ProcessName.Should().Be(domain.ProcessName);
            response.CurrentState.Should().Be(domain.CurrentState);
            response.PreviousStates.Should().BeEquivalentTo(domain.PreviousStates);
        }
    }
}
