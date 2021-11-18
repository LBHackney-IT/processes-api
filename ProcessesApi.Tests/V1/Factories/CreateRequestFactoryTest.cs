using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Factories;
using Xunit;
using FluentAssertions;
using System;

namespace ProcessesApi.Tests.V1.Factories
{
    public class CreateRequestFactoryTest
    {
        [Fact]
        public void CanMapACreateProcessRequestToADatabaseEntityObject()
        {
            var request = new CreateProcessQuery();
            var processDb = request.ToDatabase();

            processDb.CurrentState.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            processDb.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            processDb.TargetId.Should().Be(request.TargetId);
            processDb.RelatedEntities.Should().BeEquivalentTo(request.RelatedEntities);
            processDb.CurrentState.ProcessData.FormData.Should().Be(request.FormData);
            processDb.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(request.Documents);
        }
    }
}
