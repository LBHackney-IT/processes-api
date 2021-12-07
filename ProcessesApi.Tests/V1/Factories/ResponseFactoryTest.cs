using AutoFixture;
using FluentAssertions;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using Xunit;

namespace ProcessesApi.Tests.V1.Factories
{
    public class ResponseFactoryTest
    {
        private readonly Fixture _fixture = new Fixture();

        [Fact]
        public void CanMapADatabaseEntityToADomainObject()
        {
            var domain = _fixture.Create<Process>();
            var response = domain.ToResponse();

            response.Id.Should().Be(domain.Id);
            response.TargetId.Should().Be(domain.TargetId);
            response.RelatedEntities.Should().BeEquivalentTo(domain.RelatedEntities);
            response.ProcessName.Should().Be(domain.ProcessName);
            response.CurrentState.Should().Be(domain.CurrentState);
            response.PreviousStates.Should().BeEquivalentTo(domain.PreviousStates);
        }
    }
}
