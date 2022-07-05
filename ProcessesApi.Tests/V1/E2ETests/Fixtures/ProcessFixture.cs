using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.SimpleNotificationService;
using AutoFixture;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure;
using ProcessesApi.V1.Infrastructure.Extensions;
using ProcessesApi.V1.Constants.SoleToJoint;
using ProcessesApi.V1.Constants;

namespace ProcessesApi.Tests.V1.E2E.Fixtures
{
    public class ProcessFixture : IDisposable
    {
        public readonly Fixture _fixture = new Fixture();
        public readonly IDynamoDBContext _dbContext;
        private readonly IAmazonSimpleNotificationService _amazonSimpleNotificationService;
        public Process Process { get; private set; }
        public Guid ProcessId { get; private set; }
        public ProcessName ProcessName { get; private set; }
        public CreateProcess CreateProcessRequest { get; private set; }
        public UpdateProcessQuery UpdateProcessRequest { get; private set; }
        public UpdateProcessRequestObject UpdateProcessRequestObject { get; private set; }
        public ProcessQuery UpdateProcessByIdRequest { get; private set; }
        public UpdateProcessByIdRequestObject UpdateProcessByIdRequestObject { get; private set; }
        public Guid IncomingTenantId { get; private set; }
        public Guid TenantId { get; private set; }
        public List<Guid> PersonTenures { get; private set; }

        public ProcessFixture(IDynamoDBContext context, IAmazonSimpleNotificationService amazonSimpleNotificationService)
        {
            _dbContext = context;
            PersonTenures = new List<Guid> { Guid.NewGuid() };
            _amazonSimpleNotificationService = amazonSimpleNotificationService;

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
                if (Process != null)
                    _dbContext.DeleteAsync<ProcessesDb>(Process.Id).GetAwaiter().GetResult();
                _disposed = true;
            }
        }

        private void createProcess(string state)
        {
            var process = _fixture.Build<Process>()
                        .With(x => x.ProcessName, ProcessName.soletojoint)
                        .With(x => x.CurrentState,
                                _fixture.Build<ProcessState>()
                                        .With(x => x.State, state)
                                        .Create())
                        .With(x => x.VersionNumber, (int?) null)
                        .With(x => x.RelatedEntities, new List<RelatedEntity>())
                        .Create();
            Process = process;
            ProcessId = process.Id;
            ProcessName = process.ProcessName;
            IncomingTenantId = Guid.NewGuid();
            TenantId = Guid.NewGuid();

            Process.RelatedEntities.Add(new RelatedEntity
            {
                Id = IncomingTenantId,
                TargetType = TargetType.person,
                SubType = SubType.householdMember,
                Description = "Some name"
            });
        }

        public async Task GivenASoleToJointProcessExists(string state)
        {
            createProcess(state);

            await _dbContext.SaveAsync<ProcessesDb>(Process.ToDatabase()).ConfigureAwait(false);
            Process.VersionNumber = 0;
        }

        public async Task GivenASoleToJointProcessExistsWithoutRelatedEntities(string state)
        {
            createProcess(state);
            Process.RelatedEntities = new List<RelatedEntity>();

            await _dbContext.SaveAsync<ProcessesDb>(Process.ToDatabase()).ConfigureAwait(false);
            Process.VersionNumber = 0;
        }

        public void GivenASoleToJointProcessDoesNotExist()
        {
            createProcess(SharedStates.ApplicationInitialised);
        }

        public void GivenANewSoleToJointProcessRequest()
        {
            CreateProcessRequest = _fixture.Build<CreateProcess>()
                                .Create();
            ProcessName = ProcessName.soletojoint;
        }

        public void GivenANewSoleToJointProcessRequestWithValidationErrors()
        {
            CreateProcessRequest = _fixture.Build<CreateProcess>()
                            .With(x => x.TargetId, Guid.Empty)
                            .Create();
            ProcessName = ProcessName.soletojoint;
        }

        public void GivenAnUpdateSoleToJointProcessRequest(string trigger)
        {
            UpdateProcessRequest = new UpdateProcessQuery
            {
                Id = Process.Id,
                ProcessName = Process.ProcessName,
                ProcessTrigger = trigger
            };
            UpdateProcessRequestObject = _fixture.Create<UpdateProcessRequestObject>();
        }

        public void GivenACloseProcessRequestWithoutReason()
        {
            GivenAnUpdateSoleToJointProcessRequest(SharedPermittedTriggers.CloseProcess);
            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.HasNotifiedResident, true);
        }

        public void GivenACloseProcessRequestWithReason()
        {
            GivenAnUpdateSoleToJointProcessRequest(SharedPermittedTriggers.CloseProcess);
            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.HasNotifiedResident, true);
            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.Reason, "This is a reason");
        }

        public void GivenACancelProcessRequest()
        {
            GivenAnUpdateSoleToJointProcessRequest(SharedPermittedTriggers.CancelProcess);
            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.Comment, "This is a comment");
        }

        public void GivenACheckAutomatedEligibilityRequest()
        {
            GivenAnUpdateSoleToJointProcessRequest(SoleToJointPermittedTriggers.CheckAutomatedEligibility);
            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.IncomingTenantId, IncomingTenantId);
            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.TenantId, TenantId);
        }

        public void GivenACheckAutomatedEligibilityRequestWithMissingData()
        {
            GivenACheckAutomatedEligibilityRequest();
            UpdateProcessRequestObject.FormData.Remove(SoleToJointFormDataKeys.IncomingTenantId);
        }


        public void GivenACheckManualEligibilityRequest(bool isEligible)
        {
            GivenAnUpdateSoleToJointProcessRequest(SoleToJointPermittedTriggers.CheckManualEligibility);

            UpdateProcessRequestObject.FormData = new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.BR11, isEligible.ToString() },
                { SoleToJointFormDataKeys.BR12, "false" },
                { SoleToJointFormDataKeys.BR13, "false" },
                { SoleToJointFormDataKeys.BR15, "false" },
                { SoleToJointFormDataKeys.BR16, "false" },
                { SoleToJointFormDataKeys.BR7, "false"},
                { SoleToJointFormDataKeys.BR8, "false"}
            };
        }


        public void GivenAFailingCheckManualEligibilityRequest()
        {
            GivenACheckManualEligibilityRequest(false);
        }

        public void GivenAPassingCheckManualEligibilityRequest()
        {
            GivenACheckManualEligibilityRequest(true);
        }

        public void GivenACheckManualEligibilityRequestWithMissingData()
        {
            GivenACheckManualEligibilityRequest(true);
            UpdateProcessRequestObject.FormData.Remove(SoleToJointFormDataKeys.BR11);
        }

        public void GivenATenancyBreachCheckRequest(bool isEligible)
        {
            GivenAnUpdateSoleToJointProcessRequest(SoleToJointPermittedTriggers.CheckTenancyBreach);

            UpdateProcessRequestObject.FormData = new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.BR5, (!isEligible).ToString() },
                { SoleToJointFormDataKeys.BR10, "false" },
                { SoleToJointFormDataKeys.BR17, "false" },
                { SoleToJointFormDataKeys.BR18, "false" }
            };
        }

        public void GivenARequestDocumentsDesRequest()
        {
            GivenAnUpdateSoleToJointProcessRequest(SharedPermittedTriggers.RequestDocumentsDes);
        }

        public void GivenARequestDocumentsAppointmentRequest()
        {
            GivenAnUpdateSoleToJointProcessRequest(SharedPermittedTriggers.RequestDocumentsAppointment);

            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());
        }

        public async Task GivenARescheduleDocumentsAppointmentRequest()
        {
            GivenAnUpdateSoleToJointProcessRequest(SharedPermittedTriggers.RescheduleDocumentsAppointment);

            Process.CurrentState.ProcessData.FormData.Add(SoleToJointFormDataKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());

            await _dbContext.SaveAsync(Process.ToDatabase()).ConfigureAwait(false);
            Process.VersionNumber++;

            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.AppointmentDateTime, DateTime.UtcNow.AddDays(1).ToIsoString());
        }

        public void GivenAFailingCheckBreachEligibilityRequest()
        {
            GivenATenancyBreachCheckRequest(false);
        }

        public void GivenAPassingCheckBreachEligibilityRequest()
        {
            GivenATenancyBreachCheckRequest(true);
        }

        public void GivenACheckBreachEligibilityRequestWithMissingData()
        {
            GivenATenancyBreachCheckRequest(true);
            UpdateProcessRequestObject.FormData.Remove(SoleToJointFormDataKeys.BR5);
        }

        public void GivenARequestDocumentsAppointmentRequestWithMissingData()
        {
            GivenARequestDocumentsAppointmentRequest();
            UpdateProcessRequestObject.FormData.Remove(SoleToJointFormDataKeys.AppointmentDateTime);
        }
        public void GivenAnUpdateSoleToJointProcessRequestWithValidationErrors()
        {
            GivenAnUpdateSoleToJointProcessRequest(SoleToJointPermittedTriggers.CheckAutomatedEligibility);
            UpdateProcessRequestObject.Documents.Add(Guid.Empty);
        }

        public void GivenAnUpdateProcessByIdRequestWithValidationErrors()
        {
            GivenAnUpdateProcessByIdRequest();
            UpdateProcessByIdRequestObject.ProcessData.Documents.Add(Guid.Empty);
        }



        public void GivenAReviewDocumentsRequest()
        {
            GivenAnUpdateSoleToJointProcessRequest(SharedPermittedTriggers.ReviewDocuments);

            UpdateProcessRequestObject.FormData = new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.SeenPhotographicId, "true" },
                { SoleToJointFormDataKeys.SeenSecondId, "true" },
                { SoleToJointFormDataKeys.IsNotInImmigrationControl, "true" },
                {SoleToJointFormDataKeys.SeenProofOfRelationship, "true" },
                { SoleToJointFormDataKeys.IncomingTenantLivingInProperty, "true" }
            };
        }

        public void GivenAReviewDocumentsRequestWithMissingData()
        {
            GivenAReviewDocumentsRequest();
            UpdateProcessRequestObject.FormData.Remove(SoleToJointFormDataKeys.IncomingTenantLivingInProperty);
        }

        public void GivenATenureInvestigationRequest(string tenureInvestigationRecommendation)
        {
            GivenAnUpdateSoleToJointProcessRequest(SharedPermittedTriggers.TenureInvestigation);

            UpdateProcessRequestObject.FormData = new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.TenureInvestigationRecommendation, tenureInvestigationRecommendation }
            };
        }

        public void GivenATenureInvestigationRequestWithMissingData()
        {
            GivenAnUpdateSoleToJointProcessRequest(SharedPermittedTriggers.TenureInvestigation);
        }

        public void GivenATenureInvestigationRequestWithInvalidData()
        {
            GivenATenureInvestigationRequest("invalid value");
        }


        public void GivenAnUpdateProcessByIdRequest()
        {
            UpdateProcessByIdRequest = new ProcessQuery
            {
                ProcessName = ProcessName.soletojoint,
                Id = ProcessId
            };
            UpdateProcessByIdRequestObject = _fixture.Create<UpdateProcessByIdRequestObject>();
        }

        public void GivenAScheduleInterviewRequest()
        {
            GivenAnUpdateSoleToJointProcessRequest(SharedPermittedTriggers.ScheduleInterview);

            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());
        }

        public void GivenARequestScheduleInterviewRequestWithMissingData()
        {
            GivenAScheduleInterviewRequest();
            UpdateProcessRequestObject.FormData.Remove(SoleToJointFormDataKeys.AppointmentDateTime);
        }

        public void GivenARescheduleInterviewRequest()
        {
            GivenAnUpdateSoleToJointProcessRequest(SharedPermittedTriggers.RescheduleInterview);

            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());
        }

        public void GivenARequestRescheduleInterviewRequestWithMissingData()
        {
            GivenARescheduleInterviewRequest();
            UpdateProcessRequestObject.FormData.Remove(SoleToJointFormDataKeys.AppointmentDateTime);
        }

        public void GivenAHOApprovalRequest(string housingOfficerRecommendation)
        {
            GivenAnUpdateSoleToJointProcessRequest(SharedPermittedTriggers.HOApproval);

            UpdateProcessRequestObject.FormData = new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.HORecommendation, housingOfficerRecommendation },
                { SoleToJointFormDataKeys.HousingAreaManagerName, "ManagerName" }
            };
        }

        public void GivenAHOApprovalRequestWithMissingData()
        {
            GivenAnUpdateSoleToJointProcessRequest(SharedPermittedTriggers.HOApproval);
        }

        public void GivenAHOApprovalRequestWithInvalidData()
        {
            GivenAHOApprovalRequest("invalid value");
        }

        public void GivenAScheduleTenureAppointmentRequest()
        {
            GivenAnUpdateSoleToJointProcessRequest(SoleToJointPermittedTriggers.ScheduleTenureAppointment);

            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());
        }

        public void GivenARequestTenureAppointmentRequestWithMissingData()
        {
            GivenAScheduleTenureAppointmentRequest();
            UpdateProcessRequestObject.FormData.Remove(SoleToJointFormDataKeys.AppointmentDateTime);
        }

        public void GivenARescheduleTenureAppointmentRequest()
        {
            GivenAnUpdateSoleToJointProcessRequest(SoleToJointPermittedTriggers.RescheduleTenureAppointment);

            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());
        }

        public void GivenARescheduleTenureAppointmentRequestWithMissingData()
        {
            GivenARescheduleTenureAppointmentRequest();
            UpdateProcessRequestObject.FormData.Remove(SoleToJointFormDataKeys.AppointmentDateTime);
        }
        public void GivenAUpdateTenureRequest()
        {
            GivenAnUpdateSoleToJointProcessRequest(SoleToJointPermittedTriggers.UpdateTenure);
            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.HasNotifiedResident, true);
            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.Reason, "This is a reason");
        }
    }
}
