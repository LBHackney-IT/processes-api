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
using ProcessesApi.V1.Constants.ChangeOfName;

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
                        .With(x => x.ProcessName, ProcessName.soleToJoint)
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
            CreateProcessRequest = _fixture.Create<CreateProcess>();
            ProcessName = ProcessName.soleToJoint;
        }

        public void GivenANewChangeOfNameProcessRequest()
        {
            CreateProcessRequest = _fixture.Create<CreateProcess>();
            CreateProcessRequest.FormData.Add(ChangeOfNameKeys.NameSubmitted, "newName");
            ProcessName = ProcessName.changeOfName;
        }

        public void GivenANewSoleToJointProcessRequestWithValidationErrors()
        {
            CreateProcessRequest = _fixture.Build<CreateProcess>()
                            .With(x => x.TargetId, Guid.Empty)
                            .Create();
            ProcessName = ProcessName.soleToJoint;
        }

        public void GivenAnUpdateProcessRequest(string trigger)
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
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.CloseProcess);
            UpdateProcessRequestObject.FormData.Add(SharedKeys.HasNotifiedResident, true);
        }

        public void GivenACloseProcessRequestWithReason()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.CloseProcess);
            UpdateProcessRequestObject.FormData.Add(SharedKeys.HasNotifiedResident, true);
            UpdateProcessRequestObject.FormData.Add(SharedKeys.Reason, "This is a reason");
        }

        public void GivenACancelProcessRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.CancelProcess);
            UpdateProcessRequestObject.FormData.Add(SharedKeys.Comment, "This is a comment");
        }

        public void GivenACheckAutomatedEligibilityRequest()
        {
            GivenAnUpdateProcessRequest(SoleToJointPermittedTriggers.CheckAutomatedEligibility);
            UpdateProcessRequestObject.FormData.Add(SoleToJointKeys.IncomingTenantId, IncomingTenantId);
            UpdateProcessRequestObject.FormData.Add(SoleToJointKeys.TenantId, TenantId);
        }

        public void GivenACheckAutomatedEligibilityRequestWithMissingData()
        {
            GivenACheckAutomatedEligibilityRequest();
            UpdateProcessRequestObject.FormData.Remove(SoleToJointKeys.IncomingTenantId);
        }


        public void GivenACheckManualEligibilityRequest(bool isEligible)
        {
            GivenAnUpdateProcessRequest(SoleToJointPermittedTriggers.CheckManualEligibility);

            UpdateProcessRequestObject.FormData = new Dictionary<string, object>
            {
                { SoleToJointKeys.BR11, isEligible.ToString() },
                { SoleToJointKeys.BR12, "false" },
                { SoleToJointKeys.BR13, "false" },
                { SoleToJointKeys.BR15, "false" },
                { SoleToJointKeys.BR16, "false" },
                { SoleToJointKeys.BR7, "false"},
                { SoleToJointKeys.BR8, "false"}
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
            UpdateProcessRequestObject.FormData.Remove(SoleToJointKeys.BR11);
        }

        public void GivenATenancyBreachCheckRequest(bool isEligible)
        {
            GivenAnUpdateProcessRequest(SoleToJointPermittedTriggers.CheckTenancyBreach);

            UpdateProcessRequestObject.FormData = new Dictionary<string, object>
            {
                { SoleToJointKeys.BR5, (!isEligible).ToString() },
                { SoleToJointKeys.BR10, "false" },
                { SoleToJointKeys.BR17, "false" },
                { SoleToJointKeys.BR18, "false" }
            };
        }

        public void GivenARequestDocumentsDesRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.RequestDocumentsDes);
        }

        public void GivenARequestDocumentsAppointmentRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.RequestDocumentsAppointment);

            UpdateProcessRequestObject.FormData.Add(SoleToJointKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());
        }

        public async Task GivenARescheduleDocumentsAppointmentRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.RescheduleDocumentsAppointment);

            Process.CurrentState.ProcessData.FormData.Add(SoleToJointKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());

            await _dbContext.SaveAsync(Process.ToDatabase()).ConfigureAwait(false);
            Process.VersionNumber++;

            UpdateProcessRequestObject.FormData.Add(SoleToJointKeys.AppointmentDateTime, DateTime.UtcNow.AddDays(1).ToIsoString());
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
            UpdateProcessRequestObject.FormData.Remove(SoleToJointKeys.BR5);
        }

        public void GivenARequestDocumentsAppointmentRequestWithMissingData()
        {
            GivenARequestDocumentsAppointmentRequest();
            UpdateProcessRequestObject.FormData.Remove(SoleToJointKeys.AppointmentDateTime);
        }
        public void GivenAnUpdateSoleToJointProcessRequestWithValidationErrors()
        {
            GivenAnUpdateProcessRequest(SoleToJointPermittedTriggers.CheckAutomatedEligibility);
            UpdateProcessRequestObject.Documents.Add(Guid.Empty);
        }

        public void GivenAnUpdateProcessByIdRequestWithValidationErrors()
        {
            GivenAnUpdateProcessByIdRequest();
            UpdateProcessByIdRequestObject.ProcessData.Documents.Add(Guid.Empty);
        }



        public void GivenAReviewDocumentsRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.ReviewDocuments);

            UpdateProcessRequestObject.FormData = new Dictionary<string, object>
            {
                { SoleToJointKeys.SeenPhotographicId, "true" },
                { SoleToJointKeys.SeenSecondId, "true" },
                { SoleToJointKeys.IsNotInImmigrationControl, "true" },
                {SoleToJointKeys.SeenProofOfRelationship, "true" },
                { SoleToJointKeys.IncomingTenantLivingInProperty, "true" }
            };
        }

        public void GivenAReviewDocumentsRequestWithMissingData()
        {
            GivenAReviewDocumentsRequest();
            UpdateProcessRequestObject.FormData.Remove(SoleToJointKeys.IncomingTenantLivingInProperty);
        }

        public void GivenATenureInvestigationRequest(string tenureInvestigationRecommendation)
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.TenureInvestigation);

            UpdateProcessRequestObject.FormData = new Dictionary<string, object>
            {
                { SoleToJointKeys.TenureInvestigationRecommendation, tenureInvestigationRecommendation }
            };
        }

        public void GivenATenureInvestigationRequestWithMissingData()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.TenureInvestigation);
        }

        public void GivenATenureInvestigationRequestWithInvalidData()
        {
            GivenATenureInvestigationRequest("invalid value");
        }


        public void GivenAnUpdateProcessByIdRequest()
        {
            UpdateProcessByIdRequest = new ProcessQuery
            {
                ProcessName = ProcessName.soleToJoint,
                Id = ProcessId
            };
            UpdateProcessByIdRequestObject = _fixture.Create<UpdateProcessByIdRequestObject>();
        }

        public void GivenAScheduleInterviewRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.ScheduleInterview);

            UpdateProcessRequestObject.FormData.Add(SoleToJointKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());
        }

        public void GivenARequestScheduleInterviewRequestWithMissingData()
        {
            GivenAScheduleInterviewRequest();
            UpdateProcessRequestObject.FormData.Remove(SoleToJointKeys.AppointmentDateTime);
        }

        public void GivenARescheduleInterviewRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.RescheduleInterview);

            UpdateProcessRequestObject.FormData.Add(SoleToJointKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());
        }

        public void GivenARequestRescheduleInterviewRequestWithMissingData()
        {
            GivenARescheduleInterviewRequest();
            UpdateProcessRequestObject.FormData.Remove(SoleToJointKeys.AppointmentDateTime);
        }

        public void GivenAHOApprovalRequest(string housingOfficerRecommendation)
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.HOApproval);

            UpdateProcessRequestObject.FormData = new Dictionary<string, object>
            {
                { SoleToJointKeys.HORecommendation, housingOfficerRecommendation },
                { SoleToJointKeys.HousingAreaManagerName, "ManagerName" }
            };
        }

        public void GivenAHOApprovalRequestWithMissingData()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.HOApproval);
        }

        public void GivenAHOApprovalRequestWithInvalidData()
        {
            GivenAHOApprovalRequest("invalid value");
        }

        public void GivenAScheduleTenureAppointmentRequest()
        {
            GivenAnUpdateProcessRequest(SoleToJointPermittedTriggers.ScheduleTenureAppointment);

            UpdateProcessRequestObject.FormData.Add(SoleToJointKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());
        }

        public void GivenARequestTenureAppointmentRequestWithMissingData()
        {
            GivenAScheduleTenureAppointmentRequest();
            UpdateProcessRequestObject.FormData.Remove(SoleToJointKeys.AppointmentDateTime);
        }

        public void GivenARescheduleTenureAppointmentRequest()
        {
            GivenAnUpdateProcessRequest(SoleToJointPermittedTriggers.RescheduleTenureAppointment);

            UpdateProcessRequestObject.FormData.Add(SoleToJointKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());
        }

        public void GivenARescheduleTenureAppointmentRequestWithMissingData()
        {
            GivenARescheduleTenureAppointmentRequest();
            UpdateProcessRequestObject.FormData.Remove(SoleToJointKeys.AppointmentDateTime);
        }
        public void GivenAUpdateTenureRequest()
        {
            GivenAnUpdateProcessRequest(SoleToJointPermittedTriggers.UpdateTenure);
            UpdateProcessRequestObject.FormData.Add(SharedKeys.HasNotifiedResident, true);
            UpdateProcessRequestObject.FormData.Add(SharedKeys.Reason, "This is a reason");
        }

        public void GivenANameSubmittedRequest()
        {
            GivenAnUpdateProcessRequest(ChangeOfNamePermittedTriggers.EnterNewName);
            UpdateProcessRequestObject.FormData.Add(ChangeOfNameKeys.NameSubmitted, "newName");
        }
    }
}
