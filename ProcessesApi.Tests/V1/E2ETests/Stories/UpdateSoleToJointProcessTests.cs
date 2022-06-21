using Hackney.Core.Testing.DynamoDb;
using System;
using ProcessesApi.Tests.V1.E2E.Fixtures;
using ProcessesApi.Tests.V1.E2E.Steps;
using TestStack.BDDfy;
using Xunit;
using ProcessesApi.V1.Domain;
using Hackney.Core.Testing.Sns;
using ProcessesApi.V1.Domain.SoleToJoint;

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
                _processFixture?.Dispose();
                _personFixture?.Dispose();
                _tenureFixture?.Dispose();
                _agreementsApiFixture?.Dispose();
                _tenanciesApiFixture?.Dispose();
                _snsFixture?.PurgeAllQueueMessages();

                _disposed = true;
            }
        }

        [Fact]
        public void UpdateProcessReturnsNotFoundWhenProcessDoesNotExist()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessDoesNotExist())
                    .And(a => _processFixture.GivenACheckAutomatedEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenNotFoundIsReturned())
                .BDDfy();
        }

        [Fact]
        public void UpdateProcessReturnsBadRequestWhenThereAreValidationErrors()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SharedProcessStates.ApplicationInitialised))
                .And(a => _tenureFixture.GivenATenureDoesNotExist())
                .And(a => _personFixture.GivenAnAdultPersonExists(_processFixture.IncomingTenantId))
                .And(a => _processFixture.GivenAnUpdateSoleToJointProcessRequestWithValidationErrors())
            .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
            .Then(t => _steps.ThenBadRequestIsReturned())
            .BDDfy();
        }

        [Theory]
        [InlineData(109084)]
        [InlineData(null)]
        public void UpdateProcessReturnsConflictExceptionWhenTheIncorrectVersionNumberIsInIfMatchHeader(int? ifMatch)
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SharedProcessStates.ApplicationInitialised))
                    .And(a => _processFixture.GivenACheckAutomatedEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, ifMatch))
                .Then(t => _steps.ThenVersionConflictExceptionIsReturned(ifMatch))
                .BDDfy();
        }

        #region Close or Cancel a Process 

        // List all states that CloseProcess can be triggered from
        [Theory]
        [InlineData(SoleToJointStates.AutomatedChecksFailed)]
        [InlineData(SoleToJointStates.ManualChecksFailed)]
        [InlineData(SoleToJointStates.BreachChecksFailed)]
        public void ProcessStateIsUpdatedToProcessClosedWithReason(string fromState)
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(fromState))
                    .And(a => _processFixture.GivenACloseProcessRequestWithReason())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToProcessClosed(_processFixture.UpdateProcessRequest, fromState))
                    .And(a => _steps.ThenTheProcessClosedEventIsRaisedWithReason(_snsFixture, _processFixture.ProcessId))
                .BDDfy();
        }

        [Theory]
        [InlineData(SoleToJointStates.AutomatedChecksFailed)]
        [InlineData(SoleToJointStates.ManualChecksFailed)]
        [InlineData(SoleToJointStates.BreachChecksFailed)]
        public void ProcessStateIsUpdatedToProcessClosedWithoutReason(string fromState)
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(fromState))
                    .And(a => _processFixture.GivenACloseProcessRequestWithoutReason())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToProcessClosed(_processFixture.UpdateProcessRequest, fromState))
                    .And(a => _steps.ThenTheProcessClosedEventIsRaisedWithoutReason(_snsFixture, _processFixture.ProcessId))
                .BDDfy();
        }

        // List all states that CancelProcess can be triggered from
        [Theory]
        [InlineData(SoleToJointStates.DocumentsRequestedDes)]
        [InlineData(SoleToJointStates.DocumentsRequestedAppointment)]
        [InlineData(SoleToJointStates.DocumentsAppointmentRescheduled)]
        [InlineData(SoleToJointStates.HOApprovalPassed)]
        [InlineData(SoleToJointStates.HOApprovalFailed)]
        [InlineData(SoleToJointStates.InterviewScheduled)]
        [InlineData(SoleToJointStates.InterviewRescheduled)]
        [InlineData(SoleToJointStates.TenureAppointmentScheduled)]
        [InlineData(SoleToJointStates.TenureAppointmentRescheduled)]
        public void ProcessStateIsUpdatedToProcessCancelled(string fromState)
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(fromState))
                    .And(a => _processFixture.GivenACancelProcessRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToProcessCancelled(_processFixture.UpdateProcessRequest, fromState))
                    .And(a => _steps.ThenTheProcessClosedEventIsRaisedWithComment(_snsFixture, _processFixture.ProcessId))
                .BDDfy();
        }

        #endregion

        #region Automatic eligibility checks
        [Fact]
        public void InternalServerErrorIsReturnedWhenTenureDoesNotExist()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.SelectTenants))
                    .And(a => _tenureFixture.GivenATenureDoesNotExist())
                    .And(a => _personFixture.GivenAnAdultPersonExists(_processFixture.IncomingTenantId))
                    .And(a => _processFixture.GivenACheckAutomatedEligibilityRequest())
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
                    .And(a => _processFixture.GivenACheckAutomatedEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenInternalServerErrorIsReturned())
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenAutomatedEligibilityCheckDataIsMissing()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.SelectTenants))
                    .And(a => _processFixture.GivenACheckAutomatedEligibilityRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

        [Fact]
        public void ProcessStateIsUpdatedToAutomatedEligibilityChecksPassed()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.SelectTenants))
                    .And(a => _tenureFixture.GivenASecureTenureExists(_processFixture.Process.TargetId, _processFixture.TenantId, true))
                    .And(a => _tenureFixture.GivenAPersonIsAddedAsAHouseholdMember(_processFixture.IncomingTenantId))
                    .And(a => _personFixture.GivenAnAdultPersonExists(_processFixture.IncomingTenantId))
                    .And(a => _personFixture.GivenAPersonHasAnActiveTenure(_processFixture.Process.TargetId))
                    //.And(a => _agreementsApiFixture.GivenAPaymentAgreementDoesNotExist())
                    //.And(a => _tenanciesApiFixture.GivenTheTenancyHasAnInactiveNoticeOfSeekingPossession(_tenureFixture.tenancyRef))
                    .And(a => _processFixture.GivenACheckAutomatedEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheIncomingTenantIdIsAddedToRelatedEntities(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, _personFixture.Person))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToAutomatedEligibilityChecksPassed(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, SoleToJointStates.SelectTenants, SoleToJointStates.AutomatedChecksPassed))
                .BDDfy();
        }

        [Fact]
        public void ProcessStateIsUpdatedToAutomatedEligibilityChecksFailed()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.SelectTenants))
                    .And(a => _tenureFixture.GivenASecureTenureExists(_processFixture.Process.TargetId, _processFixture.TenantId, true))
                    .And(a => _tenureFixture.GivenAPersonIsAddedAsAHouseholdMember(_processFixture.IncomingTenantId))
                    .And(a => _personFixture.GivenAnAdultPersonDoesNotExist(_processFixture.IncomingTenantId))
                    .And(a => _personFixture.GivenAPersonHasAnActiveTenure(_processFixture.Process.TargetId))
                    //.And(a => _agreementsApiFixture.GivenAPaymentAgreementDoesNotExist())
                    //.And(a => _tenanciesApiFixture.GivenTheTenancyHasAnActiveNoticeOfSeekingPossession(_tenureFixture.tenancyRef))
                    .And(a => _processFixture.GivenACheckAutomatedEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheIncomingTenantIdIsAddedToRelatedEntities(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, _personFixture.Person))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToAutomatedEligibilityChecksFailed(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, SoleToJointStates.SelectTenants, SoleToJointStates.AutomatedChecksFailed))
                .BDDfy();
        }

        #endregion

        #region Manual eligibility checks

        [Fact]
        public void ProcessStateIsUpdatedToManualChecksPassed()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.AutomatedChecksPassed))
                    .And(a => _processFixture.GivenAPassingCheckManualEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToManualChecksPassed(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, SoleToJointStates.AutomatedChecksPassed, SoleToJointStates.ManualChecksPassed))
                .BDDfy();
        }

        [Fact]
        public void ProcessStateIsUpdatedToManualChecksFailed()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.AutomatedChecksPassed))
                    .And(a => _processFixture.GivenAFailingCheckManualEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToManualChecksFailed(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, SoleToJointStates.AutomatedChecksPassed, SoleToJointStates.ManualChecksFailed))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenManualEligibilityCheckDataIsMissing()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.SelectTenants))
                    .And(a => _processFixture.GivenACheckManualEligibilityRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

        #endregion

        #region Breach of tenancy checks

        [Fact]
        public void ProcessStateIsUpdatedToBreachChecksPassed()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.ManualChecksPassed))
                    .And(a => _processFixture.GivenAPassingCheckBreachEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToBreachChecksPassed(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, SoleToJointStates.ManualChecksPassed, SoleToJointStates.BreachChecksPassed))
                .BDDfy();
        }

        [Fact]
        public void ProcessStateIsUpdatedToBreachChecksFailed()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.ManualChecksPassed))
                    .And(a => _processFixture.GivenAFailingCheckBreachEligibilityRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToBreachChecksFailed(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, SoleToJointStates.ManualChecksPassed, SoleToJointStates.BreachChecksFailed))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenBreachCheckDataIsMissing()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.SelectTenants))
                    .And(a => _processFixture.GivenACheckBreachEligibilityRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }
        #endregion

        #region Documents Requested Des

        [Fact]
        public void ProcessStateIsUpdatedToDocumentsRequestedDes()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.BreachChecksPassed))
                    .And(a => _processFixture.GivenARequestDocumentsDesRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessStateIsUpdatedToDocumentsRequestedDes(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, SoleToJointStates.BreachChecksPassed, SoleToJointStates.DocumentsRequestedDes))
                .BDDfy();
        }

        #endregion

        #region Documents Requested Appointment

        [Fact]
        public void ProcessStateIsUpdatedToDocumentsRequestedAppointment()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.BreachChecksPassed))
                    .And(a => _processFixture.GivenARequestDocumentsAppointmentRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToDocumentsRequestedAppointment(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SoleToJointStates.BreachChecksPassed, SoleToJointStates.DocumentsRequestedAppointment))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenDocumentsAppointmentDataIsMissing()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.SelectTenants))
                    .And(a => _processFixture.GivenARequestDocumentsAppointmentRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }
        #endregion

        #region Document Appointment Rescheduled

        [Fact]
        public void ProcessStateIsUpdatedToDocumentsAppointmentRescheduled()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.DocumentsRequestedAppointment))
                    .And(a => _processFixture.GivenARescheduleDocumentsAppointmentRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, _processFixture.Process.VersionNumber))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToDocumentsAppointmentRescheduled(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SoleToJointStates.DocumentsRequestedAppointment, SoleToJointStates.DocumentsAppointmentRescheduled))
                .BDDfy();
        }

        [Fact]
        public void MultipleDocumentsAppointmentReschedulesArePermitted()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.DocumentsAppointmentRescheduled))
                    .And(a => _processFixture.GivenARescheduleDocumentsAppointmentRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, _processFixture.Process.VersionNumber))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateRemainsDocumentsAppointmentRescheduled(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SoleToJointStates.DocumentsAppointmentRescheduled, SoleToJointStates.DocumentsAppointmentRescheduled))
                .BDDfy();
        }
        #endregion

        #region Review documents

        [Theory]
        [InlineData(SoleToJointStates.DocumentsRequestedDes)]
        [InlineData(SoleToJointStates.DocumentsRequestedAppointment)]
        [InlineData(SoleToJointStates.DocumentsAppointmentRescheduled)]
        public void ProcessStateIsUpdatedToDocumentsChecksPassed(string initialState)
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(initialState))
                    .And(a => _processFixture.GivenAReviewDocumentsRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToDocumentChecksPassed(_processFixture.UpdateProcessRequest, initialState))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, initialState, SoleToJointStates.DocumentChecksPassed))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenRequestDocumentsCheckDataIsMissing()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.DocumentsRequestedDes))
                    .And(a => _processFixture.GivenAReviewDocumentsRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

        #endregion

        #region Tenure Investigation

        [Fact]
        public void ProcessStateIsUpdatedToApplicationSubmitted()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.DocumentChecksPassed))
                    .And(a => _processFixture.GivenAnUpdateSoleToJointProcessRequest(SoleToJointPermittedTriggers.SubmitApplication))
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessStateIsUpdatedToApplicationSubmitted(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, SoleToJointStates.DocumentChecksPassed, SoleToJointStates.ApplicationSubmitted))
                .BDDfy();
        }

        [Theory]
        [InlineData(SoleToJointFormDataValues.Appointment, SoleToJointStates.TenureInvestigationPassedWithInt)]
        [InlineData(SoleToJointFormDataValues.Approve, SoleToJointStates.TenureInvestigationPassed)]
        [InlineData(SoleToJointFormDataValues.Decline, SoleToJointStates.TenureInvestigationFailed)]
        public void ProcessStateIsUpdatedToShowResultOfTenureInvestigation(string tenureInvestigationRecommendation, string destinationState)
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.ApplicationSubmitted))
                    .And(a => _processFixture.GivenATenureInvestigationRequest(tenureInvestigationRecommendation))
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessStateIsUpdatedToShowResultsOfTenureInvestigation(_processFixture.UpdateProcessRequest, destinationState))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, SoleToJointStates.ApplicationSubmitted, destinationState))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenTenureInvestigationRecommendationIsMissing()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.ApplicationSubmitted))
                    .And(a => _processFixture.GivenATenureInvestigationRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenTenureInvestigationRecommendationIsInvalid()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.ApplicationSubmitted))
                    .And(a => _processFixture.GivenATenureInvestigationRequestWithInvalidData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

        #endregion

        #region Schedule Interview

        [Fact]
        public void ProcessStateIsUpdatedToInterviewScheduled()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.TenureInvestigationPassedWithInt))
                    .And(a => _processFixture.GivenAScheduleInterviewRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessStateIsUpdatedToInterviewScheduled(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SoleToJointStates.TenureInvestigationPassedWithInt, SoleToJointStates.InterviewScheduled))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenScheduleInterviewAppointmentDataIsMissing()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.TenureInvestigationPassed))
                    .And(a => _processFixture.GivenARequestScheduleInterviewRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }
        #endregion

        #region Reschedule Interview

        [Fact]
        public void ProcessStateIsUpdatedToInterviewRescheduled()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.InterviewScheduled))
                    .And(a => _processFixture.GivenARescheduleInterviewRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessStateIsUpdatedToInterviewRescheduled(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SoleToJointStates.InterviewScheduled, SoleToJointStates.InterviewRescheduled))
                .BDDfy();
        }

        [Fact]
        public void MultipleInterviewReschedulesArePermitted()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.InterviewRescheduled))
                    .And(a => _processFixture.GivenARescheduleInterviewRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, _processFixture.Process.VersionNumber))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateRemainsInterviewRescheduled(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SoleToJointStates.InterviewRescheduled, SoleToJointStates.InterviewRescheduled))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenRescheduleInterviewAppointmentDataIsMissing()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.InterviewScheduled))
                    .And(a => _processFixture.GivenARequestRescheduleInterviewRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }
        #endregion

        #region HOApproval

        [Theory]
        [InlineData(SoleToJointFormDataValues.Approve, SoleToJointStates.HOApprovalPassed, SoleToJointStates.InterviewScheduled)]
        [InlineData(SoleToJointFormDataValues.Decline, SoleToJointStates.HOApprovalFailed, SoleToJointStates.InterviewScheduled)]
        [InlineData(SoleToJointFormDataValues.Approve, SoleToJointStates.HOApprovalPassed, SoleToJointStates.InterviewRescheduled)]
        [InlineData(SoleToJointFormDataValues.Decline, SoleToJointStates.HOApprovalFailed, SoleToJointStates.InterviewRescheduled)]
        [InlineData(SoleToJointFormDataValues.Approve, SoleToJointStates.HOApprovalPassed, SoleToJointStates.TenureInvestigationPassed)]
        [InlineData(SoleToJointFormDataValues.Approve, SoleToJointStates.HOApprovalPassed, SoleToJointStates.TenureInvestigationFailed)]
        [InlineData(SoleToJointFormDataValues.Approve, SoleToJointStates.HOApprovalPassed, SoleToJointStates.TenureInvestigationPassedWithInt)]
        [InlineData(SoleToJointFormDataValues.Decline, SoleToJointStates.HOApprovalFailed, SoleToJointStates.TenureInvestigationPassed)]
        [InlineData(SoleToJointFormDataValues.Decline, SoleToJointStates.HOApprovalFailed, SoleToJointStates.TenureInvestigationFailed)]
        [InlineData(SoleToJointFormDataValues.Decline, SoleToJointStates.HOApprovalFailed, SoleToJointStates.TenureInvestigationPassedWithInt)]
        public void ProcessStateIsUpdatedToShowResultOfHOApproval(string housingOfficerRecommendation, string destinationState, string initialState)
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(initialState))
                    .And(a => _processFixture.GivenAHOApprovalRequest(housingOfficerRecommendation))
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessStateIsUpdatedToShowResultsOfHOApproval(_processFixture.UpdateProcessRequest, destinationState, initialState))
                .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, initialState, destinationState))
                .BDDfy();
        }

        [Theory]
        [InlineData(SoleToJointStates.InterviewScheduled)]
        [InlineData(SoleToJointStates.InterviewRescheduled)]
        public void BadRequestIsReturnedWhenHORecommendationIsMissing(string initialState)
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(initialState))
                    .And(a => _processFixture.GivenAHOApprovalRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

        [Theory]
        [InlineData(SoleToJointStates.InterviewScheduled)]
        [InlineData(SoleToJointStates.InterviewRescheduled)]
        public void BadRequestIsReturnedWhenHORecommendationIsInvalid(string initialState)
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(initialState))
                    .And(a => _processFixture.GivenAHOApprovalRequestWithInvalidData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

        #endregion

        #region Schedule Tenure Appointment

        [Fact]
        public void ProcessStateIsUpdatedToScheduleTenureAppointment()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.HOApprovalPassed))
                    .And(a => _processFixture.GivenAScheduleTenureAppointmentRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessStateIsUpdatedToScheduleTenureAppointment(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SoleToJointStates.HOApprovalPassed, SoleToJointStates.TenureAppointmentScheduled))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenScheduleTenureAppointmentDataIsMissing()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.HOApprovalPassed))
                    .And(a => _processFixture.GivenARequestTenureAppointmentRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

        #endregion

        #region Reschedule Tenure Appointment

        [Fact]
        public void ProcessStateIsUpdatedToRescheduleTenureAppointment()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.TenureAppointmentScheduled))
                    .And(a => _processFixture.GivenARescheduleTenureAppointmentRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessStateIsUpdatedToRescheduleTenureAppointment(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SoleToJointStates.TenureAppointmentScheduled, SoleToJointStates.TenureAppointmentRescheduled))
                .BDDfy();
        }

        [Fact]
        public void MultipleTenureAppointmentReschedulesArePermitted()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.TenureAppointmentRescheduled))
                    .And(a => _processFixture.GivenARescheduleTenureAppointmentRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, _processFixture.Process.VersionNumber))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateRemainsTenureAppointmentRescheduled(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SoleToJointStates.TenureAppointmentRescheduled, SoleToJointStates.TenureAppointmentRescheduled))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenRescheduleTenureAppointmentDataIsMissing()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SoleToJointStates.TenureAppointmentScheduled))
                    .And(a => _processFixture.GivenARescheduleTenureAppointmentRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

        #endregion

        #region Update Tenure

        [Theory]
        [InlineData(SoleToJointStates.TenureAppointmentScheduled)]
        [InlineData(SoleToJointStates.TenureAppointmentRescheduled)]
        public void ProcessStateIsUpdatedToProcessCompleted(string initialState)
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(initialState))
                    .And(a => _processFixture.GivenAUpdateTenureRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToUpdateTenure(_processFixture.UpdateProcessRequest, initialState))
                    .And(a => _steps.ThenTheProcessCompletedEventIsRaised(_snsFixture, _processFixture.ProcessId))
                    .And(a => _steps.ThenTheExistingTenureHasEnded(_processFixture.Process))
                .BDDfy();
        }

        #endregion

    }
}
