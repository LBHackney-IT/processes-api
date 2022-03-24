using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Sns;
using ProcessesApi.Tests.V1.E2E.Fixtures;
using ProcessesApi.Tests.V1.E2ETests.Steps;
using ProcessesApi.V1.Domain;
using System;
using TestStack.BDDfy;
using Xunit;

namespace ProcessesApi.Tests.V1.E2E.Stories
{
    [Story(
       AsA = "Internal Hackney user (such as a Housing Officer or Area Housing Manager)",
       IWant = "The system to automatically check a tenant and an applicants eligibility for a Sole to Joint application",
       SoThat = "I can more quickly determine if I should continue with the application")]
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
                if (_processFixture != null) _processFixture.Dispose();
                _disposed = true;
            }
        }

        [Fact]
        public void UpdateProcessByIdReturnsNotFoundWhenProcessDoesNotExist()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessDoesNotExist())
                    .And(a => _processFixture.GivenAnUpdateProcessByIdRequest())
                .When(w => _steps.WhenAnUpdateProcessByIdRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessByIdRequestObject, 0))
                .Then(t => _steps.ThenNotFoundIsReturned())
                .BDDfy();
        }

        [Fact]
        public void UpdateProcessByIdSucceedsWhenProcessDoesExist()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.ApplicationInitialised))
                    .And(a => _processFixture.GivenAnUpdateProcessByIdRequest())
                .When(w => _steps.WhenAnUpdateProcessByIdRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessByIdRequestObject, 0))
                .Then(t => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessByIdRequestObject))
                .BDDfy();
        }

        [Theory]
        [InlineData(null)]
        [InlineData(5)]
        public void ServiceReturnsConflictWhenIncorrectVersionNumber(int? versionNumber)
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.ApplicationInitialised))
                .And(a => _processFixture.GivenAnUpdateProcessByIdRequest())
                .When(w => _steps.WhenAnUpdateProcessByIdRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessByIdRequestObject, versionNumber))
                .Then(t => _steps.ThenVersionConflictExceptionIsReturned(versionNumber))
                .BDDfy();
        }

        [Fact]
        public void ServiceReturnsBadRequest()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessDoesNotExist())
                .And(a => _processFixture.GivenAnUpdateSoleToJointProcessRequestWithValidationErrors())
                .When(w => _steps.WhenAnUpdateProcessByIdRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessByIdRequestObject, 0))
                .Then(r => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

    }
}
