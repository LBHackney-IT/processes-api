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
        public void CanMapADomainObjectToAResponseObject()
        {
            var domain = _fixture.Create<Process>();
            var response = domain.ToResponse();

            response.Should().BeEquivalentTo(domain, c => c.Excluding(x => x.VersionNumber));
        }
    }
}
