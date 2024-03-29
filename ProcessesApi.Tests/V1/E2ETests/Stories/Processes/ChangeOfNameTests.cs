using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Sns;
using ProcessesApi.Tests.V1.E2E.Fixtures;
using ProcessesApi.Tests.V1.E2ETests.Steps;
using Hackney.Shared.Processes.Domain.Constants.ChangeOfName;
using Hackney.Shared.Processes.Domain.Constants.Shared;
using System;
using Hackney.Shared.Processes.Domain.Constants;
using TestStack.BDDfy;
using Xunit;
using SharedPermittedTriggers = Hackney.Shared.Processes.Domain.Constants.SharedPermittedTriggers;

namespace ProcessesApi.Tests.V1.E2E.Stories
{
    [Story(
    AsA = "Internal Hackney user (such as a Housing Officer or Area Housing Manager)",
    IWant = "to enter the applicants new name",
    SoThat = "I can change the legal name of the applicant in the system")]
    [Collection("AppTest collection")]
    public class ChangeOfNameTests : IDisposable
    {
        private readonly IDynamoDbFixture _dbFixture;
        private readonly ISnsFixture _snsFixture;
        private readonly PersonFixture _personFixture;
        private readonly ProcessFixture _processFixture;
        private readonly ChangeOfNameSteps _steps;

        public ChangeOfNameTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _snsFixture = appFactory.SnsFixture;
            _personFixture = new PersonFixture(_dbFixture.DynamoDbContext);

            _processFixture = new ProcessFixture(_dbFixture.DynamoDbContext, _snsFixture.SimpleNotificationService);

            _steps = new ChangeOfNameSteps(appFactory.Client, _dbFixture);
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
                _snsFixture?.PurgeAllQueueMessages();

                _disposed = true;
            }
        }

        [Fact]
        public void UpdateProcessReturnsNotFoundWhenProcessDoesNotExist()
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessDoesNotExist())
                    .And(a => _processFixture.GivenANameSubmittedRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenNotFoundIsReturned())
                .BDDfy();
        }

        [Fact]
        public void UpdateProcessReturnsBadRequestWhenThereAreValidationErrors()
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.ApplicationInitialised))
                .And(a => _processFixture.GivenAnUpdateChangeOfNameProcessRequestWithValidationErrors())
            .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
            .Then(t => _steps.ThenBadRequestIsReturned())
            .BDDfy();
        }

        [Theory]
        [InlineData(109084)]
        [InlineData(null)]
        public void UpdateProcessReturnsConflictExceptionWhenTheIncorrectVersionNumberIsInIfMatchHeader(int? ifMatch)
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.ApplicationInitialised))
                    .And(a => _processFixture.GivenANameSubmittedRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, ifMatch))
                .Then(t => _steps.ThenVersionConflictExceptionIsReturned(ifMatch))
                .BDDfy();
        }

        #region Close Process

        // List all states that CloseProcess can be triggered from
        [Theory]
        [InlineData(SharedStates.DocumentsRequestedDes)]
        [InlineData(SharedStates.DocumentsRequestedAppointment)]
        [InlineData(SharedStates.DocumentsAppointmentRescheduled)]
        public void ProcessStateIsUpdatedToProcessClosedWithReason(string fromState)
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(fromState))
                    .And(a => _processFixture.GivenACloseProcessRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToProcessClosed(_processFixture.UpdateProcessRequest, fromState))
                    .And(a => _steps.ThenTheProcessClosedEventIsRaised(_snsFixture, _processFixture.ProcessId, _processFixture.UpdateProcessRequestObject, fromState))
                .BDDfy();
        }
        #endregion

        #region Cancel Process

        // List all states that CancelProcess can be triggered from
        [Theory]
        [InlineData(ChangeOfNameStates.NameSubmitted)]
        [InlineData(SharedStates.TenureAppointmentScheduled)]
        [InlineData(SharedStates.TenureAppointmentRescheduled)]
        public void ProcessStateIsUpdatedToProcessCancelled(string fromState)
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(fromState))
                    .And(a => _processFixture.GivenACancelProcessRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToProcessCancelled(_processFixture.UpdateProcessRequest, fromState))
                    .And(a => _steps.ThenTheProcessClosedEventIsRaisedWithComment(_snsFixture, _processFixture.ProcessId, fromState))
                .BDDfy();
        }
        #endregion

        #region NameSubmitted

        [Fact]
        public void UpdateProcessToNameSubmitted()
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(ChangeOfNameStates.EnterNewName))
                .And(a => _processFixture.GivenANameSubmittedRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, ChangeOfNameStates.EnterNewName, ChangeOfNameStates.NameSubmitted))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenNoNewNameIsGiven()
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(ChangeOfNameStates.EnterNewName))
                .And(a => _processFixture.GivenANameSubmittedRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();

        }

        #endregion#

        #region DocumentsRequestedDes

        [Fact]
        public void UpdateProcessToDocumentsRequestedDes()
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(ChangeOfNameStates.NameSubmitted))
                .And(a => _processFixture.GivenARequestDocumentsDesRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, ChangeOfNameStates.NameSubmitted, SharedStates.DocumentsRequestedDes))
                .BDDfy();
        }

        #endregion

        #region Documents Requested Appointment

        [Theory]
        [InlineData(ChangeOfNameStates.NameSubmitted)]
        [InlineData(SharedStates.DocumentsRequestedDes)]

        public void ProcessStateIsUpdatedToDocumentsRequestedAppointment(string initialState)
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(initialState))
                    .And(a => _processFixture.GivenARequestDocumentsAppointmentRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToDocumentsRequestedAppointment(_processFixture.UpdateProcessRequest, initialState))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, initialState, SharedStates.DocumentsRequestedAppointment))
                .BDDfy();
        }

        [Theory]
        [InlineData(ChangeOfNameStates.NameSubmitted)]
        [InlineData(SharedStates.DocumentsRequestedDes)]
        public void BadRequestIsReturnedWhenDocumentsAppointmentDataIsMissing(string initialState)
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(initialState))
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
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.DocumentsRequestedAppointment))
                    .And(a => _processFixture.GivenARescheduleDocumentsAppointmentRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, _processFixture.Process.VersionNumber))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToDocumentsAppointmentRescheduled(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SharedStates.DocumentsRequestedAppointment, SharedStates.DocumentsAppointmentRescheduled))
                .BDDfy();
        }

        [Fact]
        public void MultipleDocumentsAppointmentReschedulesArePermitted()
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.DocumentsAppointmentRescheduled))
                    .And(a => _processFixture.GivenARescheduleDocumentsAppointmentRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, _processFixture.Process.VersionNumber))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateRemainsDocumentsAppointmentRescheduled(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SharedStates.DocumentsAppointmentRescheduled, SharedStates.DocumentsAppointmentRescheduled))
                .BDDfy();
        }
        #endregion

        #region Review documents

        [Theory]
        [InlineData(SharedStates.DocumentsRequestedDes)]
        [InlineData(SharedStates.DocumentsRequestedAppointment)]
        [InlineData(SharedStates.DocumentsAppointmentRescheduled)]
        public void ProcessStateIsUpdatedToDocumentsChecksPassed(string initialState)
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(initialState))
                    .And(a => _processFixture.GivenACONReviewDocumentsRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToDocumentChecksPassed(_processFixture.UpdateProcessRequest, initialState))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, initialState, SharedStates.DocumentChecksPassed))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenRequestDocumentsCheckDataIsMissing()
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.DocumentsRequestedDes))
                    .And(a => _processFixture.GivenACONReviewDocumentsRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

        #endregion

        #region Submit Application

        [Fact]
        public void ProcessStateIsUpdatedToApplicationSubmitted()
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.DocumentChecksPassed))
                    .And(a => _processFixture.GivenAnUpdateProcessRequest(SharedPermittedTriggers.SubmitApplication))
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessStateIsUpdatedToApplicationSubmitted(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, SharedStates.DocumentChecksPassed, SharedStates.ApplicationSubmitted))
                .BDDfy();
        }
        #endregion

        #region Tenure Investigation

        [Theory]
        [InlineData(SharedValues.Appointment, SharedStates.TenureInvestigationPassedWithInt)]
        [InlineData(SharedValues.Approve, SharedStates.TenureInvestigationPassed)]
        [InlineData(SharedValues.Decline, SharedStates.TenureInvestigationFailed)]
        public void ProcessStateIsUpdatedToShowResultOfTenureInvestigation(string tenureInvestigationRecommendation, string destinationState)
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.ApplicationSubmitted))
                    .And(a => _processFixture.GivenATenureInvestigationRequest(tenureInvestigationRecommendation))
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessStateIsUpdatedToShowResultsOfTenureInvestigation(_processFixture.UpdateProcessRequest, destinationState))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaised(_snsFixture, _processFixture.ProcessId, SharedStates.ApplicationSubmitted, destinationState))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenTenureInvestigationRecommendationIsMissing()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SharedStates.ApplicationSubmitted))
                    .And(a => _processFixture.GivenATenureInvestigationRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenTenureInvestigationRecommendationIsInvalid()
        {
            this.Given(g => _processFixture.GivenASoleToJointProcessExists(SharedStates.ApplicationSubmitted))
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
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.TenureInvestigationPassedWithInt))
                    .And(a => _processFixture.GivenAScheduleInterviewRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessStateIsUpdatedToInterviewScheduled(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SharedStates.TenureInvestigationPassedWithInt, SharedStates.InterviewScheduled))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenScheduleInterviewAppointmentDataIsMissing()
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.TenureInvestigationPassed))
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
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.InterviewScheduled))
                    .And(a => _processFixture.GivenARescheduleInterviewRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessStateIsUpdatedToInterviewRescheduled(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SharedStates.InterviewScheduled, SharedStates.InterviewRescheduled))
                .BDDfy();
        }

        [Fact]
        public void MultipleInterviewReschedulesArePermitted()
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.InterviewRescheduled))
                    .And(a => _processFixture.GivenARescheduleInterviewRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, _processFixture.Process.VersionNumber))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateRemainsInterviewRescheduled(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SharedStates.InterviewRescheduled, SharedStates.InterviewRescheduled))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenRescheduleInterviewAppointmentDataIsMissing()
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.InterviewScheduled))
                    .And(a => _processFixture.GivenARequestRescheduleInterviewRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }
        #endregion

        #region HO Approval

        [Theory]
        [InlineData(SharedValues.Approve, SharedStates.HOApprovalPassed, SharedStates.InterviewScheduled)]
        [InlineData(SharedValues.Approve, SharedStates.HOApprovalPassed, SharedStates.InterviewRescheduled)]
        [InlineData(SharedValues.Decline, SharedStates.HOApprovalFailed, SharedStates.InterviewScheduled)]
        [InlineData(SharedValues.Decline, SharedStates.HOApprovalFailed, SharedStates.InterviewRescheduled)]
        [InlineData(SharedValues.Approve, SharedStates.HOApprovalPassed, SharedStates.TenureInvestigationPassed)]
        [InlineData(SharedValues.Approve, SharedStates.HOApprovalPassed, SharedStates.TenureInvestigationFailed)]
        [InlineData(SharedValues.Approve, SharedStates.HOApprovalPassed, SharedStates.TenureInvestigationPassedWithInt)]
        [InlineData(SharedValues.Decline, SharedStates.HOApprovalFailed, SharedStates.TenureInvestigationPassed)]
        [InlineData(SharedValues.Decline, SharedStates.HOApprovalFailed, SharedStates.TenureInvestigationFailed)]
        [InlineData(SharedValues.Decline, SharedStates.HOApprovalFailed, SharedStates.TenureInvestigationPassedWithInt)]
        public void ProcessStateIsUpdatedToShowResultOfHOApproval(string housingOfficerRecommendation, string destinationState, string initialState)
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(initialState))
                    .And(a => _processFixture.GivenAHOApprovalRequest(housingOfficerRecommendation))
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessStateIsUpdatedToShowResultsOfHOApproval(_processFixture.UpdateProcessRequest, destinationState, initialState))
                .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithHOApprovalDetails(_snsFixture, _processFixture.ProcessId, _processFixture.UpdateProcessRequestObject, initialState, destinationState))
                .BDDfy();
        }

        [Theory]
        [InlineData(SharedStates.HOApprovalPassed)]
        [InlineData(SharedStates.HOApprovalFailed)]
        [InlineData(SharedStates.InterviewScheduled)]
        [InlineData(SharedStates.InterviewRescheduled)]
        public void BadRequestIsReturnedWhenHORecommendationIsMissing(string initialState)
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(initialState))
                    .And(a => _processFixture.GivenAHOApprovalRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

        [Theory]
        [InlineData(SharedStates.InterviewScheduled)]
        [InlineData(SharedStates.InterviewRescheduled)]
        [InlineData(SharedStates.HOApprovalPassed)]
        [InlineData(SharedStates.HOApprovalFailed)]
        public void BadRequestIsReturnedWhenHORecommendationIsInvalid(string initialState)
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(initialState))
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
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.HOApprovalPassed))
                    .And(a => _processFixture.GivenAScheduleTenureAppointmentRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessStateIsUpdatedToScheduleTenureAppointment(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SharedStates.HOApprovalPassed, SharedStates.TenureAppointmentScheduled))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenScheduleTenureAppointmentDataIsMissing()
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.HOApprovalPassed))
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
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.TenureAppointmentScheduled))
                    .And(a => _processFixture.GivenARescheduleTenureAppointmentRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessStateIsUpdatedToRescheduleTenureAppointment(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SharedStates.TenureAppointmentScheduled, SharedStates.TenureAppointmentRescheduled))
                .BDDfy();
        }

        [Fact]
        public void MultipleTenureAppointmentReschedulesArePermitted()
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.TenureAppointmentRescheduled))
                    .And(a => _processFixture.GivenARescheduleTenureAppointmentRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, _processFixture.Process.VersionNumber))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateRemainsTenureAppointmentRescheduled(_processFixture.UpdateProcessRequest))
                    .And(a => _steps.ThenTheProcessUpdatedEventIsRaisedWithAppointmentDetails(_snsFixture, _processFixture.ProcessId, SharedStates.TenureAppointmentRescheduled, SharedStates.TenureAppointmentRescheduled))
                .BDDfy();
        }

        [Fact]
        public void BadRequestIsReturnedWhenRescheduleTenureAppointmentDataIsMissing()
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExists(SharedStates.TenureAppointmentScheduled))
                    .And(a => _processFixture.GivenARescheduleTenureAppointmentRequestWithMissingData())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(t => _steps.ThenBadRequestIsReturned())
                .BDDfy();
        }

        #endregion

        #region UpdateName

        [Theory]
        [InlineData(SharedStates.TenureAppointmentScheduled)]
        [InlineData(SharedStates.TenureAppointmentRescheduled)]
        public void ProcessStateIsUpdatedToProcessCompleted(string initialState)
        {
            this.Given(g => _processFixture.GivenAChangeOfNameProcessExistsWithPreviousState(initialState))
                    .And(a => _personFixture.GivenAPersonExists(_processFixture.Process.TargetId))
                    .And(a => _processFixture.GivenAUpdateNameRequest())
                .When(w => _steps.WhenAnUpdateProcessRequestIsMade(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject, 0))
                .Then(a => _steps.ThenTheProcessDataIsUpdated(_processFixture.UpdateProcessRequest, _processFixture.UpdateProcessRequestObject))
                    .And(a => _steps.ThenTheProcessStateIsUpdatedToUpdateName(_processFixture.UpdateProcessRequest, initialState))
                    .And(a => _steps.ThenTheProcessCompletedEventIsRaised(_snsFixture, _processFixture.ProcessId, initialState, ChangeOfNameStates.NameUpdated))
                .BDDfy();
        }

        #endregion
    }
}
