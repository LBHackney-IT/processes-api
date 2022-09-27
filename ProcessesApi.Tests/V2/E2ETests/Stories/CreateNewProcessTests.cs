using System;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Sns;
using ProcessesApi.Tests.V2.E2E.Fixtures;
using ProcessesApi.Tests.V2.E2E.Steps;
using TestStack.BDDfy;
using Xunit;

namespace ProcessesApi.Tests.V2.E2E.Stories
{
    [Story(
        AsA = "Internal Hackney user (such as a Housing Officer or Area Housing Manager)",
         IWant = "I want to be able to initiate a Sole to Joint tenancy from a tenure record",
         SoThat = "I can initiate a tenancy change for an existing Hackney tenant")]
    [Collection("AppTest collection")]
    public class CreateNewProcessTests : IDisposable
    {
        private readonly ProcessFixture _processFixture;
        private readonly ISnsFixture _snsFixture;
        private readonly IDynamoDbFixture _dbFixture;
        private readonly CreateNewProcessSteps _steps;

        public CreateNewProcessTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _snsFixture = appFactory.SnsFixture;
            _processFixture = new ProcessFixture(_dbFixture.DynamoDbContext, _snsFixture.SimpleNotificationService);
            _steps = new CreateNewProcessSteps(appFactory.Client, _dbFixture);
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
        public void CreateNewProcessReturnsBadRequestWhenThereAreValidationErrors()
        {
            this.Given(g => _processFixture.GivenANewSoleToJointProcessRequestWithValidationErrors())
                .When(w => _steps.WhenACreateProcessRequestIsMade(_processFixture.CreateProcessRequest, _processFixture.ProcessName))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

        [Fact]
        public void CreateNewSoleToJointProcessSavesTheRequestedProcessToDatabase()
        {
            this.Given(g => _processFixture.GivenANewSoleToJointProcessRequest())
                .When(w => _steps.WhenACreateProcessRequestIsMade(_processFixture.CreateProcessRequest, _processFixture.ProcessName))
                .Then(t => _steps.ThenProcessStartedEventIsRaised(_processFixture, _snsFixture))
                    .And(t => _steps.ThenTheSoleToJointProcessIsCreated(_processFixture.CreateProcessRequest))
                .BDDfy();
        }

        [Fact]
        public void CreateNewChangeOfNameProcessSavesTheRequestedProcessToDatabase()
        {
            this.Given(g => _processFixture.GivenANewChangeOfNameProcessRequest())
                .When(w => _steps.WhenACreateProcessRequestIsMade(_processFixture.CreateProcessRequest, _processFixture.ProcessName))
                .Then(t => _steps.ThenProcessStartedEventIsRaised(_processFixture, _snsFixture))
                    .And(t => _steps.ThenTheChangeOfNameProcessIsCreated(_processFixture.CreateProcessRequest))
                .BDDfy();
        }
    }
}
