using AutoFixture;
using FluentAssertions;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure;
using Xunit;

namespace ProcessesApi.Tests.V1.Factories
{
    public class EntityFactoryTest
    {
        private readonly Fixture _fixture = new Fixture();

        [Fact]
        public void CanMapADatabaseEntityToADomainObject()
        {

            var databaseEntity = _fixture.Create<ProcessesDb>();
            var domain = databaseEntity.ToDomain();

            domain.Should().BeEquivalentTo(databaseEntity);
        }

        [Fact]
        public void CanMapADomainEntityToADatabaseObject()
        {

            var domain = _fixture.Create<Process>();
            var databaseEntity = domain.ToDatabase();

            databaseEntity.Should().BeEquivalentTo(domain);
        }
    }
}
