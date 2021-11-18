using AutoFixture;
using FluentAssertions;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Net;
using System.Threading.Tasks;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure;
using Xunit;
using System.Net.Http;
using System.Text;
using ProcessesApi.V1.Boundary.Response;

namespace ProcessesApi.Tests.V1.E2ETests
{
    [Collection("DynamoDb collection")]
    public class CreateNewProcessEndToEndTests : IDisposable
    {

        private readonly Fixture _fixture = new Fixture();
        private readonly DynamoDbIntegrationTests<Startup> _dbFixture;
        private readonly List<Action> _cleanupActions = new List<Action>();

        public CreateNewProcessEndToEndTests(DynamoDbIntegrationTests<Startup> dbFixture)
        {
            _dbFixture = dbFixture;
        }
        private CreateProcessQuery ConstructQuery()
        {
            var entity = _fixture.Build<CreateProcessQuery>()
                                .Create();
            return entity;
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
        public async Task CreateNewProcessReturnsTheRequestedProcess()
        {
            // Arrange
            var query = ConstructQuery();
            var processName = "Some-process";
            var uri = new Uri($"api/v1/process/{processName}/", UriKind.Relative);

            var message = new HttpRequestMessage(HttpMethod.Post, uri);
            message.Content = new StringContent(JsonConvert.SerializeObject(query), Encoding.UTF8, "application/json");
            message.Method = HttpMethod.Post;

            // Act
            var response = await _dbFixture.Client.SendAsync(message).ConfigureAwait(false);
            var responseContent = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            var apiProcess = JsonConvert.DeserializeObject<ProcessesResponse>(responseContent);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.Created);
            apiProcess.Id.Should().NotBeEmpty();

            var dbRecord = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(apiProcess.Id).ConfigureAwait(false);
            dbRecord.Should().BeEquivalentTo(query.ToDatabase(), c => c.Excluding(x => x.VersionNumber)
                                                                       .Excluding(y => y.ProcessName)
                                                                       .Excluding(z => z.CurrentState.CreatedAt)
                                                                       .Excluding(a => a.CurrentState.UpdatedAt));
            dbRecord.ProcessName.Should().Be(processName);
            dbRecord.CurrentState.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);
            dbRecord.CurrentState.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, 2000);

            // Cleanup
            _cleanupActions.Add(async () => await _dbFixture.DynamoDbContext.DeleteAsync<ProcessesDb>(dbRecord.Id).ConfigureAwait(false));
            message.Dispose();
        }

        [Fact]
        public async Task CreateNewProcessReturnsBadRequestWhenThereAreValidationErrors()
        {
            var badRequest = _fixture.Build<CreateProcessQuery>()
                            .With(x => x.TargetId, Guid.Empty)
                            .Create();

            var processName = "Some-process";
            var uri = new Uri($"api/v1/process/{processName}/", UriKind.Relative);
            var message = new HttpRequestMessage(HttpMethod.Post, uri);
            message.Content = new StringContent(JsonConvert.SerializeObject(badRequest), Encoding.UTF8, "application/json");
            message.Method = HttpMethod.Post;

            var response = await _dbFixture.Client.SendAsync(message).ConfigureAwait(false);
            response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

            message.Dispose();
        }
    }
}
