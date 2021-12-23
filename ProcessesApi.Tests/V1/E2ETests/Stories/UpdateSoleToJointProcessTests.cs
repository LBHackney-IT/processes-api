using Hackney.Core.Testing.DynamoDb;
using System;
using ProcessesApi.Tests.V1.E2E.Fixtures;
using ProcessesApi.Tests.V1.E2E.Steps;
using TestStack.BDDfy;
using Xunit;

namespace ProcessesApi.Tests.V1.E2E.Stories
{
    [Story(
        AsA = "Internal Hackney user (such as a Housing Officer or Area Housing Manager)",
        IWant = "The system to automatically check a tenant and an applicants eligibility for a Sole to Joint application",
        SoThat = "I can more quickly determine if I should continue with the application")]
    [Collection("AppTest collection")]
    public class UpdateSoleToJointProcessTests : IDisposable
    {
        private readonly IDynamoDbFixture _dbFixture;
        private readonly ProcessFixture _processFixture;
        private readonly PersonFixture _personFixture;
        private readonly TenureFixture _tenureFixture;
        private readonly UpdateSoleToJointProcessSteps _steps;

        public UpdateSoleToJointProcessTests(MockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _processFixture = new ProcessFixture(_dbFixture.DynamoDbContext);
            _personFixture = new PersonFixture(_dbFixture.DynamoDbContext);
            _tenureFixture = new TenureFixture(_dbFixture.DynamoDbContext);
            _steps = new UpdateSoleToJointProcessSteps(appFactory.Client);
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
        public void UpdateProcessReturnsNotFoundWhenProcessDoesNotExist()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessDoesNotExist())
                .And(a => _processFixture.GivenAnUpdateSoleToJointProcessRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                .Then(t => _steps.ThenNotFoundIsReturned())
                .BDDfy();
        }

        [Fact]
        public void ProcessStateIsUpdatedToEligibilityChecksPassed()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists())
                .And(a => _tenureFixture.GivenATenureExists(_processFixture.Process.TargetId))
                .And(a => _personFixture.GivenAPersonExists(_processFixture.IncomingTenantId))
                .And(a => _processFixture.GivenAnUpdateSoleToJointProcessRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                .Then(t => _steps.ThenTheProcessStateIsUpdatedToEligibilityChecksPassed(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, _personFixture._dbContext));
        }
    }
}
