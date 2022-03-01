using Hackney.Core.Testing.DynamoDb;
using System;
using ProcessesApi.Tests.V1.E2E.Fixtures;
using ProcessesApi.Tests.V1.E2E.Steps;
using TestStack.BDDfy;
using Xunit;
using ProcessesApi.V1.Domain;
using Hackney.Core.Testing.Sns;

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
        private readonly ISnsFixture _snsFixture;
        private readonly ProcessFixture _processFixture;
        private readonly PersonFixture _personFixture;
        private readonly TenureFixture _tenureFixture;
        private readonly IncomeApiAgreementsFixture _agreementsApiFixture;
        private readonly IncomeApiTenanciesFixture _tenanciesApiFixture;
        private readonly UpdateSoleToJointProcessSteps _steps;

        public UpdateSoleToJointProcessTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _snsFixture = appFactory.SnsFixture;
            _processFixture = new ProcessFixture(_dbFixture.DynamoDbContext, _snsFixture.SimpleNotificationService);
            _personFixture = new PersonFixture(_dbFixture.DynamoDbContext);
            _tenureFixture = new TenureFixture(_dbFixture.DynamoDbContext);
            _agreementsApiFixture = new IncomeApiAgreementsFixture();
            _tenanciesApiFixture = new IncomeApiTenanciesFixture();

            _steps = new UpdateSoleToJointProcessSteps(appFactory.Client, _dbFixture);
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
                if (_personFixture != null) _personFixture.Dispose();
                if (_tenureFixture != null) _tenureFixture.Dispose();
                if (_agreementsApiFixture != null) _agreementsApiFixture.Dispose();
                if (_tenanciesApiFixture != null) _tenanciesApiFixture.Dispose();

                _disposed = true;
            }
        }

        [Fact]
        public void UpdateProcessReturnsNotFoundWhenProcessDoesNotExist()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessDoesNotExist())
                    .And(a => _processFixture.GivenACheckEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenNotFoundIsReturned())
                .BDDfy();
        }

        [Fact]
        public void UpdateProcessReturnsBadRequestWhenThereAreValidationErrors()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.ApplicationInitialised))
                .And(a => _tenureFixture.GivenATenureDoesNotExist())
                .And(a => _personFixture.GivenAPersonExistsWithTenures(_processFixture.IncomingTenantId, _processFixture.PersonTenures))
                .And(a => _processFixture.GivenAnUpdateSoleToJointProcessRequestWithValidationErrors())
            .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
            .Then(t => _steps.ThenBadRequestIsReturned())
            .BDDfy();
        }

        [Fact]
        public void InternalServerErrorIsReturnedWhenTenureDoesNotExist()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.SelectTenants))
                    .And(a => _tenureFixture.GivenATenureDoesNotExist())
                    .And(a => _personFixture.GivenAPersonExistsWithTenures(_processFixture.IncomingTenantId, _processFixture.PersonTenures))
                    .And(a => _processFixture.GivenACheckEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenInternalServerErrorIsReturned())
                .BDDfy();
        }

        [Fact]
        public void InternalServerErrorIsReturnedWhenProposedTenantDoesNotExist()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.SelectTenants))
                    .And(a => _tenureFixture.GivenASecureTenureExists(_processFixture.Process.TargetId, _processFixture.IncomingTenantId, true))
                    .And(a => _personFixture.GivenAPersonDoesNotExist())
                    .And(a => _processFixture.GivenACheckEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenInternalServerErrorIsReturned())
                .BDDfy();
        }

        [Theory]
        [InlineData(109084)]
        [InlineData(null)]
        public void UpdateProcessReturnsConflictExceptionWhenTheIncorrectVersionNumberIsInIfMatchHeader(int? ifMatch)
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.ApplicationInitialised))
                    .And(a => _processFixture.GivenACheckEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, ifMatch))
                .Then(t => _steps.ThenVersionConflictExceptionIsReturned(ifMatch))
                .BDDfy();
        }

        [Fact]
        public void ProcessStateIsUpdatedToEligibilityChecksPassed()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.SelectTenants))
                    .And(a => _tenureFixture.GivenASecureTenureExists(_processFixture.Process.TargetId, _processFixture.IncomingTenantId, true))
                    .And(a => _tenureFixture.GivenASecureTenureExists(_processFixture.PersonTenures[0], _processFixture.IncomingTenantId, false))
                    .And(a => _personFixture.GivenAPersonExistsWithTenures(_processFixture.IncomingTenantId, _processFixture.PersonTenures))
                    .And(a => _agreementsApiFixture.GivenAPaymentAgreementDoesNotExist())
                    .And(a => _tenanciesApiFixture.GivenTheTenancyHasAnInactiveNoticeOfSeekingPossession(_tenureFixture.tenancyRef))
                    .And(a => _processFixture.GivenACheckEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheIncomingTenantIdIsAddedToRelatedEntities(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToEligibilityChecksPassed(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                .BDDfy();
        }

        [Fact]
        public void ProcessStateIsUpdatedToEligibilityChecksFailed()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.SelectTenants))
                    .And(a => _tenureFixture.GivenASecureTenureExists(_processFixture.Process.TargetId, _processFixture.IncomingTenantId, true))
                    .And(a => _tenureFixture.GivenASecureTenureExists(_processFixture.PersonTenures[0], _processFixture.IncomingTenantId, false))
                    .And(a => _personFixture.GivenAPersonExistsWithTenures(_processFixture.IncomingTenantId, _processFixture.PersonTenures))
                    .And(a => _agreementsApiFixture.GivenAPaymentAgreementDoesNotExist())
                    .And(a => _tenanciesApiFixture.GivenTheTenancyHasAnActiveNoticeOfSeekingPossession(_tenureFixture.tenancyRef))
                    .And(a => _processFixture.GivenACheckEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheIncomingTenantIdIsAddedToRelatedEntities(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToEligibilityChecksFailed(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                .BDDfy();
        }

        [Fact]
        public void ProcessStateIsUpdatedToManualChecksPassed()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.AutomatedChecksPassed))
                    .And(a => _processFixture.GivenAPassingCheckManualEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToManualChecksPassed(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                .BDDfy();
        }

        [Fact]
        public void ProcessStateIsUpdatedToManualChecksFailed()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.AutomatedChecksPassed))
                    .And(a => _processFixture.GivenAFailingCheckManualEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToManualChecksFailed(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                .BDDfy();
        }
    }
}
