using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using Xunit;
using FluentAssertions;

namespace ProcessesApi.Tests.V1.Factories
{
    public class ResponseFactoryTest
    {
        [Fact]
        public void CanMapADatabaseEntityToADomainObject()
        {
            var domain = new Process();
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
