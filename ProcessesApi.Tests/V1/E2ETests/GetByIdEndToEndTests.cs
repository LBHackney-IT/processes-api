using AutoFixture;
using FluentAssertions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure;
using Xunit;

namespace ProcessesApi.Tests.V1.E2ETests
{
    [Collection("DynamoDb collection")]
    public class GetByIdEndToEndTests : IDisposable
    {

        private readonly Fixture _fixture = new Fixture();
        public ProcessesDb Technology { get; private set; }
        private readonly DynamoDbIntegrationTests<Startup> _dbFixture;
        private readonly List<Action> _cleanupActions = new List<Action>();

        public GetByIdEndToEndTests(DynamoDbIntegrationTests<Startup> dbFixture)
        {
            _dbFixture = dbFixture;
        }
        private Process ConstructTestEntity()
        {
            var entity = _fixture.Create<Process>();
            return entity;
        }

        private async Task SaveTestData(Process entity)
        {
            await _dbFixture.DynamoDbContext.SaveAsync(entity.ToDatabase()).ConfigureAwait(false);
            _cleanupActions.Add(async () => await _dbFixture.DynamoDbContext.DeleteAsync<ProcessesDb>(entity.Id.ToString()).ConfigureAwait(false));
        }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        private bool _disposed;
        protected virtual void Dispose(bool disposing)
        {
            if (disposing && !_disposed)
            {
                foreach (var action in _cleanupActions)
                    action();

                _disposed = true;
            }
        }

        [Fact]
        public async Task GetProcessByValidIdReturnsOKResponse()
        {
            // Arrange
            var entity = ConstructTestEntity();
            var processName = "Some-process";
            await SaveTestData(entity).ConfigureAwait(false);
            var uri = new Uri($"api/v1/process/{processName}/{entity.Id}", UriKind.Relative);

            // Act
            var response = await _dbFixture.Client.GetAsync(uri).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var apiEntity = JsonConvert.DeserializeObject<Process>(responseContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            apiEntity.Should().BeEquivalentTo(entity);
        }

        [Fact]
        public async Task GetProcessByInvalidIdReturnsBadRequestResponse()
        {
            // Arrange
            var badId = _fixture.Create<int>();
            var processName = "Some-process";
            var uri = new Uri($"api/v1/process/{processName}/{badId}", UriKind.Relative);
            // Act
            var response = await _dbFixture.Client.GetAsync(uri).ConfigureAwait(false);
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        }

        [Fact]
        public async Task GetProcessByNonExistentIdReturnsNotFoundResponse()
        {
            // Arrange
            var id = Guid.NewGuid();
            var processName = "Some-process";
            var uri = new Uri($"api/v1/process/{processName}/{id}", UriKind.Relative);
            // Act
            var response = await _dbFixture.Client.GetAsync(uri).ConfigureAwait(false);
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
