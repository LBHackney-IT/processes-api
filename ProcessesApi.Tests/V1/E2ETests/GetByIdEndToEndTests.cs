using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Newtonsoft.Json;
using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Xunit;

namespace ProcessesApi.Tests.V1.E2ETests
{
    [Collection("AppTest collection")]
    public class GetByIdEndToEndTests : IDisposable
    {

        private readonly Fixture _fixture = new Fixture();
        private readonly IDynamoDbFixture _dbFixture;
        private readonly HttpClient _httpClient;

        public GetByIdEndToEndTests(MockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _httpClient = appFactory.Client;
        }
        private Process ConstructTestEntity()
        {
            var entity = _fixture.Build<Process>()
                                .With(x => x.VersionNumber, (int?) null)
                                .Create();
            return entity;
        }

        private async Task SaveTestData(Process entity)
        {
            await _dbFixture.SaveEntityAsync(entity.ToDatabase()).ConfigureAwait(false);
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
            var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var apiEntity = JsonConvert.DeserializeObject<ProcessResponse>(responseContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            apiEntity.Should().BeEquivalentTo(entity, config => config.Excluding(y => y.VersionNumber));

        }

        [Fact]
        public async Task GetProcessByInvalidIdReturnsBadRequestResponse()
        {
            // Arrange
            var badId = _fixture.Create<int>();
            var processName = "Some-process";
            var uri = new Uri($"api/v1/process/{processName}/{badId}", UriKind.Relative);
            // Act
            var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);
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
            var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
