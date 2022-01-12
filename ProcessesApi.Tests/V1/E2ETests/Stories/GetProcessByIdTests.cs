using Hackney.Core.Testing.DynamoDb;
using System;
using ProcessesApi.Tests.V1.E2E.Fixtures;
using ProcessesApi.Tests.V1.E2E.Steps;
using TestStack.BDDfy;
using Xunit;

namespace ProcessesApi.Tests.V1.E2E.Stories
{
    [Story(
         AsA = "Service",
         IWant = "an endpoint to return process details",
         SoThat = "it is possible to view the current state and previous states of a process")]
    [Collection("AppTest collection")]
    public class GetProcessByIdTests : IDisposable
    {
        private readonly IDynamoDbFixture _dbFixture;
        private readonly ProcessFixture _processFixture;
        private readonly GetProcessByIdSteps _steps;

        public GetProcessByIdTests(MockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _processFixture = new ProcessFixture(appFactory.DynamoDbFixture);
            _steps = new GetProcessByIdSteps(appFactory.Client);
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
        public void GetProcessByValidIdReturnsOKResponseWithETagHeaders()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists())
                .When(w => _steps.WhenTheProcessIsRequested(_processFixture.ProcessName, _processFixture.ProcessId))
                .Then(t => _steps.ThenTheProcessIsReturned(_processFixture.Process))
                .BDDfy();
        }

        [Fact]
        public void GetProcessByNonExistentIdReturnsNotFoundResponse()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessDoesNotExist())
                .When(w => _steps.WhenTheProcessIsRequested(_processFixture.ProcessName, _processFixture.ProcessId))
                .Then(t => _steps.ThenNotFoundIsReturned())
                .BDDfy();
        }
    }
}
