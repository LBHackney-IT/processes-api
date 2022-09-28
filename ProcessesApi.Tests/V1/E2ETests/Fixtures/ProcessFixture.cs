using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.SimpleNotificationService;
using AutoFixture;
using Hackney.Shared.Processes.Boundary.Request;
using Hackney.Shared.Processes.Domain;
using Hackney.Shared.Processes.Factories;
using Hackney.Shared.Processes.Infrastructure;
using Hackney.Shared.Processes.Infrastructure.Extensions;
using System.Linq;
using Hackney.Shared.Person.Domain;
using Hackney.Shared.Processes.Domain.Constants;
using Hackney.Shared.Processes.Domain.Constants.ChangeOfName;
using Hackney.Shared.Processes.Domain.Constants.SoleToJoint;
using Hackney.Shared.Processes.Boundary.Request.V1;

namespace ProcessesApi.Tests.V1.E2E.Fixtures
{
    public class ProcessFixture : IDisposable
    {
        public readonly Fixture _fixture = new Fixture();
        public readonly IDynamoDBContext _dbContext;
        private readonly IAmazonSimpleNotificationService _amazonSimpleNotificationService;
        public Process Process { get; private set; }
        public Guid ProcessId { get; private set; }
        public Guid TargetId { get; private set; }
        public ProcessName ProcessName { get; private set; }
        public CreateProcess CreateProcessRequest { get; private set; }
        public UpdateProcessQuery UpdateProcessRequest { get; private set; }
        public UpdateProcessRequestObject UpdateProcessRequestObject { get; private set; }
        public ProcessQuery UpdateProcessByIdRequest { get; private set; }
        public UpdateProcessByIdRequestObject UpdateProcessByIdRequestObject { get; private set; }
        public Guid IncomingTenantId { get; private set; }
        public Guid TenantId { get; private set; }
        public List<Guid> PersonTenures { get; private set; }
        public const int MAXPROCESSES = 10;
        public List<ProcessesDb> Processes { get; private set; } = new List<ProcessesDb>();

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

        private void createProcess(string state, ProcessName processName)
        {
            var process = _fixture.Build<Process>()
                        .With(x => x.ProcessName, processName)
                        .With(x => x.CurrentState,
                                _fixture.Build<ProcessState>()
                                        .With(x => x.State, state)
                                        .Create())
                        .With(x => x.VersionNumber, (int?) null)
                        .With(x => x.RelatedEntities, new List<RelatedEntity>())
                        .Create();
            Process = process;
            ProcessId = process.Id;
            TargetId = process.TargetId;
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
            createProcess(state, ProcessName.soletojoint);

            await _dbContext.SaveAsync<ProcessesDb>(Process.ToDatabase()).ConfigureAwait(false);
            Process.VersionNumber = 0;
        }

        public async Task GivenAChangeOfNameProcessExists(string state)
        {
            createProcess(state, ProcessName.changeofname);

            await _dbContext.SaveAsync<ProcessesDb>(Process.ToDatabase()).ConfigureAwait(false);
            Process.VersionNumber = 0;
        }

        public async Task GivenAChangeOfNameProcessExistsWithPreviousState(string state)
        {
            createProcess(state, ProcessName.changeofname);

            Process.PreviousStates.Add(_fixture.Build<ProcessState>()
                                               .With(x => x.State, ChangeOfNameStates.NameSubmitted)
                                               .Create()
                                      );
            Process.PreviousStates.Find(x => x.State == ChangeOfNameStates.NameSubmitted).ProcessData.FormData.Add(ChangeOfNameKeys.FirstName, "NewFirstName");
            Process.PreviousStates.Find(x => x.State == ChangeOfNameStates.NameSubmitted).ProcessData.FormData.Add(ChangeOfNameKeys.Surname, "newSurname");
            Process.PreviousStates.Find(x => x.State == ChangeOfNameStates.NameSubmitted).ProcessData.FormData.Add(ChangeOfNameKeys.Title, Title.Miss);
            await _dbContext.SaveAsync<ProcessesDb>(Process.ToDatabase()).ConfigureAwait(false);
            Process.VersionNumber = 0;
        }

        public async Task GivenASoleToJointProcessExistsWithoutRelatedEntities(string state)
        {
            createProcess(state, ProcessName.soletojoint);

            Process.RelatedEntities = new List<RelatedEntity>();
            Process.RelatedEntities.Add(new RelatedEntity
            {
                Id = TenantId,
                TargetType = TargetType.tenure,
                SubType = SubType.tenant,
                Description = "tenantId"
            });

            await _dbContext.SaveAsync<ProcessesDb>(Process.ToDatabase()).ConfigureAwait(false);
            Process.VersionNumber = 0;
        }

        public void GivenASoleToJointProcessDoesNotExist()
        {
            createProcess(SharedStates.ApplicationInitialised, ProcessName.soletojoint);
        }

        public void GivenAChangeOfNameProcessDoesNotExist()
        {
            createProcess(SharedStates.ApplicationInitialised, ProcessName.changeofname);
        }

        public void GivenANewSoleToJointProcessRequest()
        {
            CreateProcessRequest = _fixture.Create<CreateProcess>();
            ProcessName = ProcessName.soletojoint;
        }

        public void GivenANewChangeOfNameProcessRequest()
        {
            CreateProcessRequest = _fixture.Create<CreateProcess>();
            ProcessName = ProcessName.changeofname;

        }

        public void GivenANewSoleToJointProcessRequestWithValidationErrors()
        {
            CreateProcessRequest = _fixture.Build<CreateProcess>()
                            .With(x => x.TargetId, Guid.Empty)
                            .Create();
            ProcessName = ProcessName.soletojoint;
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

        public void GivenACloseProcessRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.CloseProcess);
            UpdateProcessRequestObject.FormData.Add(SharedKeys.HasNotifiedResident, true);

            var random = new Random();
            if (random.Next() % 2 == 0) // randomly add reason to formdata
                UpdateProcessRequestObject.FormData.Add(SharedKeys.Reason, "This is a reason");
        }

        public void GivenACancelProcessRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.CancelProcess);
            UpdateProcessRequestObject.FormData.Add(SharedKeys.Comment, "This is a comment");
        }

        public void GivenACompleteProcessRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.CompleteProcess);
            UpdateProcessRequestObject.FormData.Add(SharedKeys.HasNotifiedResident, true);
        }

        public void GivenACheckAutomatedEligibilityRequest()
        {
            GivenAnUpdateProcessRequest(SoleToJointPermittedTriggers.CheckAutomatedEligibility);
            UpdateProcessRequestObject.FormData.Add(SoleToJointKeys.IncomingTenantId, IncomingTenantId);
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
                { SoleToJointKeys.BR8, "false"},
                { SoleToJointKeys.BR9, "false"}
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
                { SoleToJointKeys.BR5, (isEligible).ToString() },
                { SoleToJointKeys.BR10, "true" },
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

            UpdateProcessRequestObject.FormData.Add(SharedKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());
        }

        public async Task GivenARescheduleDocumentsAppointmentRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.RescheduleDocumentsAppointment);

            Process.CurrentState.ProcessData.FormData.Add(SharedKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());

            await _dbContext.SaveAsync(Process.ToDatabase()).ConfigureAwait(false);
            Process.VersionNumber++;

            UpdateProcessRequestObject.FormData.Add(SharedKeys.AppointmentDateTime, DateTime.UtcNow.AddDays(1).ToIsoString());
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
            UpdateProcessRequestObject.FormData.Remove(SharedKeys.AppointmentDateTime);
        }
        public void GivenAnUpdateSoleToJointProcessRequestWithValidationErrors()
        {
            GivenAnUpdateProcessRequest(SoleToJointPermittedTriggers.CheckAutomatedEligibility);
            UpdateProcessRequestObject.Documents.Add(Guid.Empty);
        }

        public void GivenAnUpdateChangeOfNameProcessRequestWithValidationErrors()
        {
            GivenAnUpdateProcessRequest(ChangeOfNamePermittedTriggers.EnterNewName);
            UpdateProcessRequestObject.Documents.Add(Guid.Empty);
        }

        public void GivenAnUpdateProcessByIdRequestWithValidationErrors()
        {
            GivenAnUpdateProcessByIdRequest();
            UpdateProcessByIdRequestObject.ProcessData.Documents.Add(Guid.Empty);
        }



        public void GivenASTJReviewDocumentsRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.ReviewDocuments);

            UpdateProcessRequestObject.FormData = new Dictionary<string, object>
            {
                { SharedKeys.SeenPhotographicId, "true" },
                { SharedKeys.SeenSecondId, "true" },
                { SoleToJointKeys.IsNotInImmigrationControl, "true" },
                {SoleToJointKeys.SeenProofOfRelationship, "true" },
                { SoleToJointKeys.IncomingTenantLivingInProperty, "true" }
            };
        }

        public void GivenACONReviewDocumentsRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.ReviewDocuments);

            UpdateProcessRequestObject.FormData = new Dictionary<string, object>
            {
                { SharedKeys.SeenPhotographicId, "true" },
                { SharedKeys.SeenSecondId, "true" },
                { ChangeOfNameKeys.AtLeastOneDocument, "true" }
            };
        }

        public void GivenASTJReviewDocumentsRequestWithMissingData()
        {
            GivenASTJReviewDocumentsRequest();
            UpdateProcessRequestObject.FormData.Remove(SoleToJointKeys.IncomingTenantLivingInProperty);
        }

        public void GivenACONReviewDocumentsRequestWithMissingData()
        {
            GivenACONReviewDocumentsRequest();
            UpdateProcessRequestObject.FormData.Remove(ChangeOfNameKeys.AtLeastOneDocument);
        }

        public void GivenATenureInvestigationRequest(string tenureInvestigationRecommendation)
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.TenureInvestigation);

            UpdateProcessRequestObject.FormData = new Dictionary<string, object>
            {
                { SharedKeys.TenureInvestigationRecommendation, tenureInvestigationRecommendation }
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
                ProcessName = ProcessName.soletojoint,
                Id = ProcessId
            };
            UpdateProcessByIdRequestObject = _fixture.Create<UpdateProcessByIdRequestObject>();
        }

        public void GivenAScheduleInterviewRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.ScheduleInterview);

            UpdateProcessRequestObject.FormData.Add(SharedKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());
        }

        public void GivenARequestScheduleInterviewRequestWithMissingData()
        {
            GivenAScheduleInterviewRequest();
            UpdateProcessRequestObject.FormData.Remove(SharedKeys.AppointmentDateTime);
        }

        public void GivenARescheduleInterviewRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.RescheduleInterview);

            UpdateProcessRequestObject.FormData.Add(SharedKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());
        }

        public void GivenARequestRescheduleInterviewRequestWithMissingData()
        {
            GivenARescheduleInterviewRequest();
            UpdateProcessRequestObject.FormData.Remove(SharedKeys.AppointmentDateTime);
        }

        public void GivenAHOApprovalRequest(string housingOfficerRecommendation)
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.HOApproval);

            UpdateProcessRequestObject.FormData = new Dictionary<string, object>
            {
                { SharedKeys.HORecommendation, housingOfficerRecommendation },
                { SharedKeys.HousingAreaManagerName, "ManagerName" }
            };

            var random = new Random();
            if (random.Next() % 2 == 0) // randomly add reason to formdata
                UpdateProcessRequestObject.FormData.Add(SharedKeys.Reason, "Some Reason");
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
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.ScheduleTenureAppointment);

            UpdateProcessRequestObject.FormData.Add(SharedKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());
        }

        public void GivenARequestTenureAppointmentRequestWithMissingData()
        {
            GivenAScheduleTenureAppointmentRequest();
            UpdateProcessRequestObject.FormData.Remove(SharedKeys.AppointmentDateTime);
        }

        public void GivenARescheduleTenureAppointmentRequest()
        {
            GivenAnUpdateProcessRequest(SharedPermittedTriggers.RescheduleTenureAppointment);

            UpdateProcessRequestObject.FormData.Add(SharedKeys.AppointmentDateTime, DateTime.UtcNow.ToIsoString());
        }

        public void GivenARescheduleTenureAppointmentRequestWithMissingData()
        {
            GivenARescheduleTenureAppointmentRequest();
            UpdateProcessRequestObject.FormData.Remove(SharedKeys.AppointmentDateTime);
        }
        public void GivenAUpdateTenureRequest()
        {
            GivenAnUpdateProcessRequest(SoleToJointPermittedTriggers.UpdateTenure);
            UpdateProcessRequestObject.FormData.Add(SoleToJointKeys.TenureStartDate, DateTime.Parse(_fixture.Create<DateTime>().ToLongDateString()));
        }

        public void GivenANameSubmittedRequest()
        {
            GivenAnUpdateProcessRequest(ChangeOfNamePermittedTriggers.EnterNewName);
            UpdateProcessRequestObject.FormData.Add(ChangeOfNameKeys.FirstName, "newName");
            UpdateProcessRequestObject.FormData.Add(ChangeOfNameKeys.Surname, "newSurname");
            UpdateProcessRequestObject.FormData.Add(ChangeOfNameKeys.Title, Title.Mrs);
        }

        public void GivenANameSubmittedRequestWithMissingData()
        {
            GivenANameSubmittedRequest();
            UpdateProcessRequestObject.FormData.Clear();
        }

        public void GivenAUpdateNameRequest()
        {
            GivenAnUpdateProcessRequest(ChangeOfNamePermittedTriggers.UpdateName);
            UpdateProcessRequestObject.FormData.Add(SharedKeys.HasNotifiedResident, true);
            UpdateProcessRequestObject.FormData.Add(SharedKeys.Reason, "This is a reason");
            UpdateProcessRequestObject.FormData.Add(ChangeOfNameKeys.FirstName, "NewFirstName");
        }

        public void GivenTargetProcessesAlreadyExist()
        {
            GivenTargetProcessesAlreadyExist(MAXPROCESSES);
        }

        public void GivenTargetProcessesAlreadyExist(int count)
        {
            if (!Processes.Any())
            {
                var random = new Random();
                TargetId = Guid.NewGuid();
                Processes.AddRange(_fixture.Build<ProcessesDb>()
                                           .With(x => x.TargetType, TargetType.person)
                                           .With(x => x.TargetId, TargetId)
                                           .With(x => x.CurrentState, (ProcessState) null)
                                           .With(x => x.PreviousStates, new List<ProcessState>())
                                           .With(x => x.VersionNumber, (int?) null)
                                           .CreateMany(count));
                foreach (var process in Processes)
                    _dbContext.SaveAsync(process).GetAwaiter().GetResult();
            }
        }

        public void GivenTargetProcessesWithMultiplePagesAlreadyExist()
        {
            GivenTargetProcessesAlreadyExist(35);
        }

        public void GivenATargetIdHasNoProcesses()
        {
            TargetId = Guid.NewGuid();
        }

        public void GivenAnInvalidTargetId()
        {
            TargetId = Guid.Empty;
        }

    }
}
