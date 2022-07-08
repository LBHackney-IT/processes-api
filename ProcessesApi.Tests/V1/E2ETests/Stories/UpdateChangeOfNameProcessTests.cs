using Hackney.Core.Testing.DynamoDb;
using Hackney.Core.Testing.Sns;
using ProcessesApi.Tests.V1.E2E.Fixtures;
using ProcessesApi.Tests.V1.E2ETests.Steps;
using ProcessesApi.V1.Constants;
using ProcessesApi.V1.Constants.ChangeOfName;
using System;
using TestStack.BDDfy;
using Xunit;

namespace ProcessesApi.Tests.V1.E2E.Stories
{
    [Story(
    AsA = "Internal Hackney user (such as a Housing Officer or Area Housing Manager)",
    IWant = "to enter the applicants new name",
    SoThat = "I can change the legal name of the applicant in the system")]
    [Collection("AppTest collection")]
    public class UpdateChangeOfNameProcessTests : IDisposable
    {
        private readonly IDynamoDbFixture _dbFixture;
        private readonly ISnsFixture _snsFixture;
        private readonly ProcessFixture _processFixture;
        private readonly UpdateProcessBaseSteps _steps;

        public UpdateChangeOfNameProcessTests(AwsMockWebApplicationFactory<Startup> appFactory)
        {
            _dbFixture = appFactory.DynamoDbFixture;
            _snsFixture = appFactory.SnsFixture;
            _processFixture = new ProcessFixture(_dbFixture.DynamoDbContext, _snsFixture.SimpleNotificationService);

            _steps = new UpdateProcessBaseSteps(appFactory.Client, _dbFixture);
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



    }
}
