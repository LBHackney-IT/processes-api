using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;
using AutoFixture;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure;

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
                        .Create();
            Process = process;
            ProcessId = process.Id;
            ProcessName = process.ProcessName;
            IncomingTenantId = Guid.NewGuid();
            TenantId = Guid.NewGuid();
        }

        public async Task GivenASoleToJointProcessExists(string state)
        {
            createProcess(state);
            await _dbContext.SaveAsync<ProcessesDb>(Process.ToDatabase()).ConfigureAwait(false);
        }

        public void GivenASoleToJointProcessDoesNotExist()
        {
            createProcess(SharedProcessStates.ApplicationInitialised);
        }

        public void GivenANewSoleToJointProcessRequest()
        {
            CreateProcessRequest = _fixture.Build<CreateProcess>()
                                .Create();
            CreateSnsTopic();
            ProcessName = ProcessName.soletojoint;
        }

        public void GivenANewSoleToJointProcessRequestWithValidationErrors()
        {
            CreateProcessRequest = _fixture.Build<CreateProcess>()
                            .With(x => x.TargetId, Guid.Empty)
                            .Create();
            CreateSnsTopic();
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

        public void GivenACloseProcessRequest()
        {
            GivenAnUpdateSoleToJointProcessRequest(SoleToJointPermittedTriggers.CloseProcess);
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


        public void GivenARequestDocumentsAppointmentRequest()
        {
            GivenAnUpdateSoleToJointProcessRequest(SoleToJointPermittedTriggers.RequestDocumentsAppointment);
            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.AppointmentDateTime, _fixture.Create<DateTime>());
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
            GivenAnUpdateProcessByIdRequest(ProcessId);
            UpdateProcessByIdRequestObject.ProcessData.Documents.Add(Guid.Empty);
        }
        public void GivenAnUpdateProcessByIdRequest(Guid id)
        {
            UpdateProcessByIdRequest = _fixture.Build<ProcessQuery>()
                                           .With(x => x.ProcessName, ProcessName.soletojoint)
                                           .With(x => x.Id, id)
                                           .Create();
            UpdateProcessByIdRequestObject = _fixture.Create<UpdateProcessByIdRequestObject>();
        }

        private void CreateSnsTopic()
        {
            var snsAttrs = new Dictionary<string, string>();
            snsAttrs.Add("fifo_topic", "true");
            snsAttrs.Add("content_based_deduplication", "true");

            var response = _amazonSimpleNotificationService.CreateTopicAsync(new CreateTopicRequest
            {
                Name = "processes",
                Attributes = snsAttrs
            }).Result;

            Environment.SetEnvironmentVariable("PROCESS_SNS_ARN", response.TopicArn);
        }
    }
}
