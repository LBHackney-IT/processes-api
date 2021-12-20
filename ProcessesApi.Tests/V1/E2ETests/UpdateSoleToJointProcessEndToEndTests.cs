using AutoFixture;
using FluentAssertions;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Shared.Tenure.Domain;
using Hackney.Shared.Tenure.Factories;
using Hackney.Shared.Tenure.Infrastructure;
using Newtonsoft.Json;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
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
    public class UpdateSoleToJointProcessEndToEndTests : IDisposable
    {
        private readonly Fixture _fixture = new Fixture();
        private readonly IDynamoDbFixture _dbFixture;
        private readonly HttpClient _httpClient;
        private readonly List<Action> _cleanupActions = new List<Action>();

        public UpdateSoleToJointProcessEndToEndTests(MockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _httpClient = appFactory.Client;
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

        private UpdateProcessQueryObject ConstructQuery()
        {
            var originalEntity = _fixture.Build<UpdateProcessQueryObject>()
                                .Create();
            return originalEntity;
        }

        private Process ConstructTestEntity()
        {
            var originalEntity = _fixture.Build<Process>()
                                        .With(x => x.VersionNumber, (int?) null)
                                        .With(x => x.CurrentState,
                                                   _fixture.Build<ProcessState>()
                                                           .With(x => x.CreatedAt, DateTime.UtcNow)
                                                           .With(x => x.UpdatedAt, DateTime.UtcNow)
                                                           .With(x => x.State, SoleToJointStates.SelectTenants)
                                                           .With(x => x.PermittedTriggers, (new[] { SoleToJointPermittedTriggers.CheckEligibility }).ToList())
                                                           .Create())
                                        .Without(x => x.PreviousStates)
                                        .With(x => x.ProcessName, ProcessNamesConstants.SoleToJoint)
                                        .Create();
            return originalEntity;
        }

        private async Task SaveTestData(Process originalEntity)
        {
            await _dbFixture.SaveEntityAsync(originalEntity.ToDatabase()).ConfigureAwait(false);
        }

        private async Task<(Process, TenureInformation, Guid)> ConstructAndSaveTestData()
        {
            var tenant = _fixture.Create<HouseholdMembers>();
            var tenure = _fixture.Build<TenureInformation>()
                                 .With(x => x.HouseholdMembers, new List<HouseholdMembers> { tenant })
                                 .With(x => x.VersionNumber, (int?) null)
                                 .Create();
            var process = _fixture.Build<Process>()
                            .With(x => x.VersionNumber, (int?) null)
                            .With(x => x.CurrentState,
                                    _fixture.Build<ProcessState>()
                                            .With(x => x.CreatedAt, DateTime.UtcNow)
                                            .With(x => x.UpdatedAt, DateTime.UtcNow)
                                            .With(x => x.State, SoleToJointStates.SelectTenants)
                                            .With(x => x.PermittedTriggers, (new[] { SoleToJointPermittedTriggers.CheckEligibility }).ToList())
                                            .Create())
                            .Without(x => x.PreviousStates)
                            .With(x => x.ProcessName, ProcessNamesConstants.SoleToJoint)
                            .Create();

            await _dbFixture.SaveEntityAsync<ProcessesDb>(process.ToDatabase()).ConfigureAwait(false);
            await _dbFixture.SaveEntityAsync<TenureInformationDb>(tenure.ToDatabase()).ConfigureAwait(false);

            return (process, tenure, tenant.Id);
        }

        [Fact(Skip = "To be completed when adding another state")]
        public async Task UpdateProcessReturnsUpdatedResponse()
        {
            // Arrange
            var originalEntity = ConstructTestEntity();
            await SaveTestData(originalEntity).ConfigureAwait(false);
            //var ifMatch = 0;

            var queryObject = _fixture.Create<UpdateProcessQueryObject>();
            var query = _fixture.Build<UpdateProcessQuery>()
                                .With(x => x.Id, originalEntity.Id)
                                .With(x => x.ProcessName, originalEntity.ProcessName)
                                .With(x => x.ProcessTrigger, SoleToJointPermittedTriggers.CheckEligibility)
                                .Create();
            var uri = new Uri($"api/v1/process/{query.ProcessName}/{query.Id}/{query.ProcessTrigger}", UriKind.Relative);

            var message = new HttpRequestMessage(HttpMethod.Patch, uri);
            message.Content = new StringContent(JsonConvert.SerializeObject(queryObject), Encoding.UTF8, "application/json");
            //message.Headers.TryAddWithoutValidation(HeaderConstants.IfMatch, $"\"{ifMatch.ToString()}\"");
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
        public async Task AddTenantToRelatedEntitiesOnCheckEligibilityTrigger()
        {
            // Arrange
            (var originalProcess, var tenure, var incomingTenantId) = await ConstructAndSaveTestData().ConfigureAwait(false);
            var ifMatch = 0;

            var queryObject = _fixture.Build<UpdateProcessQueryObject>()
                            .With(x => x.FormData, new Dictionary<string, object> { { SoleToJointFormDataKeys.IncomingTenantId, incomingTenantId } })
                            .Create();
            var uri = new Uri($"api/v1/process/{originalProcess.ProcessName}/{originalProcess.Id}/{SoleToJointPermittedTriggers.CheckEligibility}", UriKind.Relative);

            var message = new HttpRequestMessage(HttpMethod.Patch, uri);
            message.Content = new StringContent(JsonConvert.SerializeObject(queryObject), Encoding.UTF8, "application/json");
            message.Headers.TryAddWithoutValidation(HeaderConstants.IfMatch, $"\"{ifMatch.ToString()}\"");
            message.Method = HttpMethod.Patch;

            // Act
            var response = await _httpClient.SendAsync(message).ConfigureAwait(false);

            // Assert
            response.StatusCode.Should().Be(HttpStatusCode.NoContent);

            var dbRecord = await _dbFixture.DynamoDbContext.LoadAsync<ProcessesDb>(originalProcess.Id).ConfigureAwait(false);
            dbRecord.RelatedEntities.Should().Contain(incomingTenantId);
            // Cleanup
            message.Dispose();
        }

        [Fact(Skip = "To be completed when adding another state")]
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

        [Fact(Skip = "To be completed when adding another state")]
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
