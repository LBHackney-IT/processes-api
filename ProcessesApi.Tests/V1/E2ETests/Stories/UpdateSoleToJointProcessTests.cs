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
        private readonly IncomeApiAgreementsFixture _agreementsApiFixture;
        private readonly IncomeApiTenanciesFixture _tenanciesApiFixture;
        private readonly UpdateSoleToJointProcessSteps _steps;

        public UpdateSoleToJointProcessTests(MockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;

            _processFixture = new ProcessFixture(_dbFixture.DynamoDbContext);
            _personFixture = new PersonFixture(_dbFixture.DynamoDbContext);
            _tenureFixture = new TenureFixture(_dbFixture.DynamoDbContext);
            _agreementsApiFixture = new IncomeApiAgreementsFixture();
            _tenanciesApiFixture = new IncomeApiTenanciesFixture();

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
                    .And(a => _processFixture.AndGivenAnUpdateSoleToJointProcessRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenNotFoundIsReturned())
                .BDDfy();
        }

        [Fact]
        public void UpdateProcessReturnsBadRequestWhenThereAreValidationErrors()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists())
                .And(g => _processFixture.AndGivenAnUpdateSoleToJointProcessRequestWithValidationErrors())
            .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
            .Then(t => _steps.ThenBadRequestIsReturned())
            .BDDfy();
        }

        [Fact]
        public void UpdateProcessReturnsBadRequestWhenTheProcessIsNotSupported()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists())
                    .And(g => _processFixture.AndGivenAnUpdateSoleToJointProcessRequestWithsAnUnSupportedProcess())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

        [Fact]
        public void InternalServerErrorIsReturnedWhenTenureDoesNotExist()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists())
                    .And(a => _tenureFixture.AndGivenATenureDoesNotExist())
                    .And(a => _personFixture.AndGivenAPersonExistsWithTenures(_processFixture.IncomingTenantId, _processFixture.PersonTenures))
                    .And(a => _processFixture.AndGivenAnUpdateSoleToJointProcessRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenInternalServerErrorIsReturned())
                .BDDfy();
        }

        [Fact]
        public void InternalServerErrorIsReturnedWhenProposedTenantDoesNotExist()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists())
                    .And(a => _tenureFixture.AndGivenATenureExists(_processFixture.Process.TargetId, _processFixture.IncomingTenantId, true))
                    .And(a => _personFixture.AndGivenAPersonDoesNotExist())
                    .And(a => _processFixture.AndGivenAnUpdateSoleToJointProcessRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenInternalServerErrorIsReturned())
                .BDDfy();
        }

        [Theory]
        [InlineData(109084)]
        [InlineData(null)]
        public void UpdateProcessReturnsConflictExceptionWhenTheIncorrectVersionNumberIsInIfMatchHeader(int? ifMatch)
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists())
                    .And(a => _tenureFixture.AndGivenATenureExists(_processFixture.Process.TargetId, _processFixture.IncomingTenantId, false))
                    .And(a => _personFixture.AndGivenAPersonExistsWithTenures(_processFixture.IncomingTenantId, _processFixture.PersonTenures))
                    .And(a => _processFixture.AndGivenAnUpdateSoleToJointProcessRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, ifMatch))
                .Then(t => _steps.ThenVersionConflictExceptionIsReturned(ifMatch))
                .BDDfy();
        }

        [Fact]
        public void ProcessStateIsUpdatedToEligibilityChecksPassed()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists())
                    .And(a => _tenureFixture.AndGivenATenureExists(_processFixture.Process.TargetId, _processFixture.IncomingTenantId, true))
                    .And(a => _tenureFixture.AndGivenATenureExists(_processFixture.PersonTenures[0], _processFixture.IncomingTenantId, false))
                    .And(a => _personFixture.AndGivenAPersonExistsWithTenures(_processFixture.IncomingTenantId, _processFixture.PersonTenures))
                    .And(a => _agreementsApiFixture.AndGivenAPaymentAgreementDoesNotExistForTenancy(_processFixture.PersonTenures[0]))
                    .And(a => _tenanciesApiFixture.AndGivenTheTenancyHasAnInactiveNoticeOfSeekingPossession(_processFixture.PersonTenures[0]))
                    .And(a => _processFixture.AndGivenAnUpdateSoleToJointProcessRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, _personFixture._dbContext))
                    .And(a => _steps.AndTheProcessStateIsUpdatedToEligibilityChecksPassed(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, _personFixture._dbContext))
                .BDDfy();
        }

        [Fact]
        public void ProcessStateIsUpdatedToEligibilityChecksFailed()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists())
                    .And(a => _tenureFixture.AndGivenANonSecureTenureExists(_processFixture.Process.TargetId, _processFixture.IncomingTenantId, true))
                    .And(a => _personFixture.AndGivenAPersonExistsWithTenures(_processFixture.IncomingTenantId, _processFixture.PersonTenures))
                    .And(a => _processFixture.AndGivenAnUpdateSoleToJointProcessRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, _personFixture._dbContext))
                    .And(a => _steps.AndTheProcessStateIsUpdatedToEligibilityChecksFailed(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, _personFixture._dbContext))
                .BDDfy();
            // simplified as IncomeApiFixture(s) are not working as expected
        }
    }
}