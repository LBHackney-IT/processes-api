using System;
using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Sns;
using ProcessesApi.Tests.V1.E2E.Fixtures;
using ProcessesApi.Tests.V1.E2E.Steps;
using TestStack.BDDfy;
using Xunit;

namespace ProcessesApi.Tests.V1.E2E.Stories
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
        private readonly CreateNewSoleToJointProcessSteps _steps;

        public CreateNewProcessTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _snsFixture = appFactory.SnsFixture;
            _processFixture = new ProcessFixture(_dbFixture.DynamoDbContext, _snsFixture.SimpleNotificationService);
            _steps = new CreateNewSoleToJointProcessSteps(appFactory.Client, _dbFixture);
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
                if (_processFixture != null)
                    _processFixture.Dispose();

                _disposed = true;
            }
        }

        [Fact]
        public void CreateNewProcessSavesTheRequestedProcessToDatabase()
        {
            this.Given(g => _processFixture.GivenANewSoleToJointProcessRequest())
                .When(w => _steps.WhenACreateProcessRequestIsMade(_processFixture.CreateProcessRequest, _processFixture.ProcessName))
                .Then(t => _steps.ThenTheProcessIsCreated(_processFixture.CreateProcessRequest))
                .BDDfy();
        }

        [Fact]
        public void CreateNewProcessReturnsBadRequestWhenThereAreValidationErrors()
        {
            this.Given(g => _processFixture.GivenANewSoleToJointProcessRequestWithValidationErrors())
                .When(w => _steps.WhenACreateProcessRequestIsMade(_processFixture.CreateProcessRequest, _processFixture.ProcessName))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }
    }
}
