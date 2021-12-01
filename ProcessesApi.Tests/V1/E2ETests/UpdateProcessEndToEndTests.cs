using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Newtonsoft.Json;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ProcessesApi.Tests.V1.E2ETests
{
    [Collection("AppTest collection")]
    public class UpdateProcessEndToEndTests : IDisposable
    {
        private readonly Fixture _fixture = new Fixture();
        private readonly IDynamoDbFixture _dbFixture;
        private readonly HttpClient _httpClient;
        private readonly List<Action> _cleanupActions = new List<Action>();

        public UpdateProcessEndToEndTests(MockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _httpClient = appFactory.Client;
        }
        private UpdateProcessQueryObject ConstructQuery()
        {
            var originalEntity = _fixture.Build<UpdateProcessQueryObject>()
                                .Create();
            return originalEntity;
        }

        private SoleToJointProcess ConstructTestEntity()
        {
            var originalEntity = _fixture.Build<SoleToJointProcess>()
                                .With(x => x.VersionNumber, (int?) null)
                                .Create();
            return originalEntity;
        }

        private async Task SaveTestData(SoleToJointProcess originalEntity)
        {
            await _dbFixture.SaveEntityAsync(originalEntity.ToDatabase()).ConfigureAwait(false);
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
        public async Task UpdateProcessReturnsUpdatedResponse()
        {
            // Arrange
            var originalEntity = ConstructTestEntity();
            await SaveTestData(originalEntity).ConfigureAwait(false);
            var ifMatch = 0;

            var queryObject = _fixture.Create<UpdateProcessQueryObject>();
            var query = _fixture.Build<UpdateProcessQuery>()
                                .With(x => x.Id, originalEntity.Id)
                                .With(x => x.ProcessName, originalEntity.ProcessName)
                                .Create();
            var uri = new Uri($"api/v1/process/{query.ProcessName}/{query.Id}/{query.ProcessTrigger}", UriKind.Relative);

            var message = new HttpRequestMessage(HttpMethod.Patch, uri);
            message.Content = new StringContent(JsonConvert.SerializeObject(queryObject), Encoding.UTF8, "application/json");
            message.Headers.TryAddWithoutValidation(HeaderConstants.IfMatch, $"\"{ifMatch.ToString()}\"");
            message.Method = HttpMethod.Patch;

            // Act
            var response = await _httpClient.SendAsync(message).ConfigureAwait(false);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var dbRecord = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(originalEntity.Id).ConfigureAwait(false);
            dbRecord.PreviousStates.LastOrDefault().Should().BeEquivalentTo(originalEntity.CurrentState, c => c.Excluding(x => x.ProcessData.FormData));
            dbRecord.CurrentState.ProcessData.Documents.Should().BeEquivalentTo(queryObject.Documents);

            // Cleanup
            message.Dispose();
        }

        [Fact]
        public async void UpdateProcessReturnsNotFoundWhenProcessDoesNotExist()
        {
            // Arrange
            var ifMatch = 0;
            var queryObject = _fixture.Create<UpdateProcessQueryObject>();
            var query = _fixture.Create<UpdateProcessQuery>();
            var uri = new Uri($"api/v1/process/{query.ProcessName}/{query.Id}/{query.ProcessTrigger}", UriKind.Relative);

            var message = new HttpRequestMessage(HttpMethod.Patch, uri);
            message.Content = new StringContent(JsonConvert.SerializeObject(queryObject), Encoding.UTF8, "application/json");
            message.Headers.TryAddWithoutValidation(HeaderConstants.IfMatch, $"\"{ifMatch.ToString()}\"");
            message.Method = HttpMethod.Patch;

            // Act
            var response = await _httpClient.SendAsync(message).ConfigureAwait(false);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NotFound);

            // Cleanup
            message.Dispose();
        }

        [Fact]
        public async Task UpdateProcessReturnsConflictExceptionWhenIncorrectVersionNumber()
        {
            // Arrange
            var originalEntity = ConstructTestEntity();
            await SaveTestData(originalEntity).ConfigureAwait(false);
            var ifMatch = 5;

            var queryObject = _fixture.Create<UpdateProcessQueryObject>();
            var query = _fixture.Build<UpdateProcessQuery>()
                                .With(x => x.Id, originalEntity.Id)
                                .With(x => x.ProcessName, originalEntity.ProcessName)
                                .Create();
            var uri = new Uri($"api/v1/process/{query.ProcessName}/{query.Id}/{query.ProcessTrigger}", UriKind.Relative);

            var message = new HttpRequestMessage(HttpMethod.Patch, uri);
            message.Content = new StringContent(JsonConvert.SerializeObject(queryObject), Encoding.UTF8, "application/json");
            message.Headers.TryAddWithoutValidation(HeaderConstants.IfMatch, $"\"{ifMatch.ToString()}\"");
            message.Method = HttpMethod.Patch;

            // Act
            var response = await _httpClient.SendAsync(message).ConfigureAwait(false);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Conflict);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            responseContent.Should().Contain($"The version number supplied ({ifMatch}) does not match the current value on the entity (0).");

            // Cleanup
            message.Dispose();
        }
    }
}
