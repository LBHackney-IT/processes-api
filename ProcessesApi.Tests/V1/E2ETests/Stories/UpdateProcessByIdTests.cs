using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Sns;
using ProcessesApi.Tests.V1.E2E.Fixtures;
using ProcessesApi.Tests.V1.E2ETests.Steps;
using System;
using Hackney.Shared.Processes.Constants;
using TestStack.BDDfy;
using Xunit;

namespace ProcessesApi.Tests.V1.E2E.Stories
{
    [Story(
       AsA = "Internal Hackney user (such as a Housing Officer or Area Housing Manager)",
       IWant = "I want to be able to update the Sole to Joint process without changing the state",
       SoThat = "I can update case details when required")]
    [Collection("AppTest collection")]
    public class UpdateProcessByIdTests : IDisposable
    {
        private readonly IDynamoDbFixture _dbFixture;
        private readonly ISnsFixture _snsFixture;
        private readonly ProcessFixture _processFixture;
        private readonly UpdateProcessByIdSteps _steps;

        public UpdateProcessByIdTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _snsFixture = appFactory.SnsFixture;
            _processFixture = new ProcessFixture(_dbFixture.DynamoDbContext, _snsFixture.SimpleNotificationService);
            _steps = new UpdateProcessByIdSteps(appFactory.Client, _dbFixture);
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
                _processFixture?.Dispose();
                _snsFixture?.PurgeAllQueueMessages();

                _disposed = true;
            }
        }

        [Fact]
        public void UpdateProcessByIdReturnsNotFoundWhenProcessDoesNotExist()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessDoesNotExist())
                    .And(a => _processFixture.GivenAnUpdateProcessByIdRequest())
                .When(w => _steps.WhenAnUpdateProcessByIdRequestIsMade(_processFixture.UpdateProcessByIdRequest, _processFixture.UpdateProcessByIdRequestObject, 0))
                .Then(t => _steps.ThenNotFoundIsReturned())
                .BDDfy();
        }

        [Fact]
        public void UpdateProcessByIdSucceedsWhenProcessDoesExist()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SharedStates.ApplicationInitialised))
                    .And(a => _processFixture.GivenAnUpdateProcessByIdRequest())
                .When(w => _steps.WhenAnUpdateProcessByIdRequestIsMade(_processFixture.UpdateProcessByIdRequest, _processFixture.UpdateProcessByIdRequestObject, 0))
                .Then(t => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessByIdRequest, _processFixture.UpdateProcessByIdRequestObject))
                   .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture))
                .BDDfy();
        }

        [Theory]
        [InlineData(null)]
        [InlineData(5)]
        public void ServiceReturnsConflictWhenIncorrectVersionNumber(int? versionNumber)
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SharedStates.ApplicationInitialised))
                .And(a => _processFixture.GivenAnUpdateProcessByIdRequest())
                .When(w => _steps.WhenAnUpdateProcessByIdRequestIsMade(_processFixture.UpdateProcessByIdRequest, _processFixture.UpdateProcessByIdRequestObject, versionNumber))
                .Then(t => _steps.ThenVersionConflictExceptionIsReturned(versionNumber))
                .BDDfy();
        }

        [Fact]
        public void ServiceReturnsBadRequest()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SharedStates.ApplicationInitialised))
                .And(a => _processFixture.GivenAnUpdateProcessByIdRequestWithValidationErrors())
                .When(w => _steps.WhenAnUpdateProcessByIdRequestIsMade(_processFixture.UpdateProcessByIdRequest, _processFixture.UpdateProcessByIdRequestObject, 0))
                .Then(r => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

    }
}
