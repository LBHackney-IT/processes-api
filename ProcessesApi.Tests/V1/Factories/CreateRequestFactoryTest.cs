using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Factories;
using Xunit;
using FluentAssertions;

namespace ProcessesApi.Tests.V1.Factories
{
    public class CreateRequestFactoryTest
    {
        [Fact]
        public void CanMapACreateProcessRequestToADatabaseEntityObject()
        {
            var request = new CreateProcessQuery();
            var domain = request.ToDatabase();

            domain.TargetId.Should().Be(request.TargetId);
            domain.RelatedEntities.Should().BeEquivalentTo(request.RelatedEntities);
            domain.ProcessName.Should().Be(request.ProcessName);
            domain.CurrentState.ProcessData.FormData.Should().Be(request.FormData);
            domain.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(request.Documents);
        }
    }
}
