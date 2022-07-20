using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Sns;
using ProcessesApi.Tests.V1.E2E.Fixtures;
using ProcessesApi.Tests.V1.E2E.Steps;
using System;
using TestStack.BDDfy;
using Xunit;

namespace ProcessesApi.Tests.V1.E2E.Stories
{
    [Story(
        AsA = "Internal Hackney user (such as a Housing Officer or Area housing Manager)",
        IWant = "to be able to view processes against a person, property or tenure",
        SoThat = "so that I know what process is active against the entity and what stage the process is at")]
    [Collection("AppTest collection")]
    public class GetProcessesByTargetIdTests : IDisposable
    {
        private readonly IDynamoDbFixture _dbFixture;
        private readonly ISnsFixture _snsFixture;
        private readonly ProcessFixture _processFixture;
        private readonly GetProcessesByTargetIdSteps _steps;

        public GetProcessesByTargetIdTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _snsFixture = appFactory.SnsFixture;
            _processFixture = new ProcessFixture(_dbFixture.DynamoDbContext, _snsFixture.SimpleNotificationService);
            _steps = new GetProcessesByTargetIdSteps(appFactory.Client);
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
                if (null != _processFixture)
                    _processFixture.Dispose();

                _disposed = true;
            }
        }

        [Fact]
        public void ServiceReturnsTheRequestedProcesses()
        {
            this.Given(g => _processFixture.GivenTargetProcessesAlreadyExist())
                .When(w => _steps.WhenTheTargetProcessesAreRequested(_processFixture.TargetId))
                .Then(t => _steps.ThenTheTargetProcessesAreReturned(_processFixture.Processes))
                .BDDfy();
        }

        [Theory]
        [InlineData(null)]
        [InlineData(5)]
        [InlineData(15)]
        [InlineData(100)]
        public void ServiceReturnsTheRequestedProcessesByPageSize(int? pageSize)
        {
            this.Given(g => _processFixture.GivenTargetProcessesAlreadyExist(30))
                .When(w => _steps.WhenTheTargetProcessesAreRequestedWithPageSize(_processFixture.TargetId, pageSize))
                .Then(t => _steps.ThenTheTargetProcessesAreReturnedByPageSize(_processFixture.Processes, pageSize))
                .BDDfy();
        }

        [Fact]
        public void ServiceReturnsFirstPageOfRequestedProcessesWithPaginationToken()
        {
            this.Given(g => _processFixture.GivenTargetProcessesWithMultiplePagesAlreadyExist())
                .When(w => _steps.WhenTheTargetProcessesAreRequested(_processFixture.TargetId))
                .Then(t => _steps.ThenTheFirstPageOfTargetProcessesAreReturned(_processFixture.Processes))
                .BDDfy();
        }

        [Fact]
        public void ServiceReturnsNoPaginationTokenIfNoMoreResults()
        {
            this.Given(g => _processFixture.GivenTargetProcessesAlreadyExist(10))
                .When(w => _steps.WhenTheTargetProcessesAreRequested(_processFixture.TargetId))
                .Then(t => _steps.ThenAllTheTargetProcessesAreReturnedWithNoPaginationToken(_processFixture.Processes))
                .BDDfy();
        }

        [Fact]
        public void ServiceReturnsAllPagesProcessesUsingPaginationToken()
        {
            this.Given(g => _processFixture.GivenTargetProcessesWithMultiplePagesAlreadyExist())
                .When(w => _steps.WhenAllTheTargetProcessesAreRequested(_processFixture.TargetId))
                .Then(t => _steps.ThenAllTheTargetProcessesAreReturned(_processFixture.Processes))
                .BDDfy();
        }

        [Fact]
        public void ServiceReturnsEmptyArrayIfNoProcessesExistForTargetId()
        {
            this.Given(g => _processFixture.GivenATargetIdHasNoProcesses())
                .When(w => _steps.WhenTheTargetProcessesAreRequested(_processFixture.TargetId))
                .Then(t => _steps.ThenAllTheTargetProcessesAreReturnedWithNoPaginationToken(_processFixture.Processes))
                .BDDfy();
        }

        [Fact]
        public void ServiceReturnsBadRequestIfIdInvalid()
        {
            this.Given(g => _processFixture.GivenAnInvalidTargetId())
                .When(w => _steps.WhenTheTargetProcessesAreRequested(_processFixture.TargetId))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }
    }
}
