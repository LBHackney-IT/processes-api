using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Newtonsoft.Json;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Response;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using System;
using System.Linq;
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
            var process = _fixture.Build<Process>()
                        .With(x => x.ProcessName, ProcessNamesConstants.SoleToJoint)
                        .With(x => x.CurrentState,
                                _fixture.Build<ProcessState>()
                                        .With(x => x.State, SoleToJointStates.ApplicationInitialised)
                                        .With(x => x.PermittedTriggers, (new[] { SoleToJointTriggers.StartApplication }).ToList())
                                        .Create())
                        .Without(x => x.PreviousStates)
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();
            return process;
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
        public async Task GetProcessByValidIdReturnsOKResponseWithETagHeaders()
        {
            // Arrange
            var entity = ConstructTestEntity();
            await SaveTestData(entity).ConfigureAwait(false);
            var uri = new Uri($"api/v1/process/{entity.ProcessName}/{entity.Id}", UriKind.Relative);

            // Act
            var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var apiEntity = JsonConvert.DeserializeObject<ProcessResponse>(responseContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.OK);
            apiEntity.Should().BeEquivalentTo(entity, config => config.Excluding(y => y.VersionNumber));

            var expectedEtagValue = $"\"{0}\"";
            response.Headers.ETag.Tag.Should().Be(expectedEtagValue);
            var eTagHeaders = response.Headers.GetValues(HeaderConstants.ETag);
            eTagHeaders.Count().Should().Be(1);
            eTagHeaders.First().Should().Be(expectedEtagValue);
        }

        [Fact]
        public async Task GetProcessByInvalidIdReturnsBadRequestResponse()
        {
            // Arrange
            var badId = _fixture.Create<int>();
            var processName = ProcessNamesConstants.SoleToJoint;
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
            var processName = ProcessNamesConstants.SoleToJoint;
            var uri = new Uri($"api/v1/process/{processName}/{id}", UriKind.Relative);
            // Act
            var response = await _httpClient.GetAsync(uri).ConfigureAwait(false);
            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        }
    }
}
