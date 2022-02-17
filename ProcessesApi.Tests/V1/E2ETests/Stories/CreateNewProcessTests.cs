using System;
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
        private readonly CreateNewSoleToJointProcessSteps _steps;

        public CreateNewProcessTests(MockWebApplicationFactory<Startup> appFactory)
        {
            _processFixture = new ProcessFixture(appFactory.DynamoDbFixture);
            _steps = new CreateNewSoleToJointProcessSteps(appFactory.Client, appFactory.DynamoDbFixture);
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
