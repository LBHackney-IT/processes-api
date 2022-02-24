using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AutoFixture;
using Hackney.Core.Testing.DynamoDb;
using ProcessesApi.V1.Boundary.Constants;
using ProcessesApi.V1.Boundary.Request;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Infrastructure;

namespace ProcessesApi.Tests.V1.E2E.Fixtures
{
    public class ProcessFixture : IDisposable
    {
        public readonly Fixture _fixture = new Fixture();
        private readonly IDynamoDbFixture _dbFixture;
        public Process Process { get; private set; }
        public string ProcessId { get; private set; }
        public string ProcessName { get; private set; }
        public CreateProcess CreateProcessRequest { get; private set; }
        public UpdateProcessQuery UpdateProcessRequest { get; private set; }
        public UpdateProcessQueryObject UpdateProcessRequestObject { get; private set; }
        public Guid IncomingTenantId { get; private set; }
        public List<Guid> PersonTenures { get; private set; }

        public ProcessFixture(IDynamoDbFixture dbFixture)
        {
            _dbFixture = dbFixture;
            PersonTenures = new List<Guid> { Guid.NewGuid() };
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
                    _dbFixture.DynamoDbContext.DeleteAsync<ProcessesDb>(Process.Id).GetAwaiter().GetResult();
                _disposed = true;
            }
        }

        private void createProcess(string state)
        {
            var process = _fixture.Build<Process>()
                        .With(x => x.ProcessName, ProcessNamesConstants.SoleToJoint)
                        .With(x => x.CurrentState,
                                _fixture.Build<ProcessState>()
                                        .With(x => x.State, state)
                                        .Create())
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();
            Process = process;
            ProcessId = process.Id.ToString();
            ProcessName = process.ProcessName;
            IncomingTenantId = Guid.NewGuid();
            PersonTenures = _fixture.CreateMany<Guid>().ToList();
        }

        public async Task GivenASoleToJointProcessExists(string state)
        {
            createProcess(state);
            await _dbFixture.SaveEntityAsync<ProcessesDb>(Process.ToDatabase()).ConfigureAwait(false);
        }

        public void GivenASoleToJointProcessDoesNotExist()
        {
            createProcess(SoleToJointStates.ApplicationInitialised);
        }

        public void GivenANewSoleToJointProcessRequest()
        {
            CreateProcessRequest = _fixture.Build<CreateProcess>()
                                .Create();
            ProcessName = ProcessNamesConstants.SoleToJoint;
        }

        public void GivenANewSoleToJointProcessRequestWithValidationErrors()
        {
            CreateProcessRequest = _fixture.Build<CreateProcess>()
                            .With(x => x.TargetId, Guid.Empty)
                            .Create();
            ProcessName = ProcessNamesConstants.SoleToJoint;
        }

        public void GivenAnUpdateSoleToJointProcessRequest(string trigger)
        {
            UpdateProcessRequest = new UpdateProcessQuery
            {
                Id = Process.Id,
                ProcessName = Process.ProcessName,
                ProcessTrigger = trigger
            };
            UpdateProcessRequestObject = _fixture.Create<UpdateProcessQueryObject>();
        }

        public void GivenACheckEligibilityRequest()
        {
            GivenAnUpdateSoleToJointProcessRequest(SoleToJointPermittedTriggers.CheckEligibility);
            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.IncomingTenantId, IncomingTenantId);
        }

        public void GivenACheckManualEligibilityRequest(string eligibilityCheckId, string value)
        {
            GivenAnUpdateSoleToJointProcessRequest(SoleToJointPermittedTriggers.CheckManualEligibility);
            UpdateProcessRequestObject.FormData = new Dictionary<string, object>
            {
                { SoleToJointFormDataKeys.BR11, "true" },
                { SoleToJointFormDataKeys.BR12, "false" },
                { SoleToJointFormDataKeys.BR13, "false" },
                { SoleToJointFormDataKeys.BR15, "false" },
                { SoleToJointFormDataKeys.BR16, "false" }
            };
            if (eligibilityCheckId != null)
                UpdateProcessRequestObject.FormData[eligibilityCheckId] = value;
        }

        public void GivenAnUpdateSoleToJointProcessRequestWithValidationErrors()
        {
            GivenAnUpdateSoleToJointProcessRequest(SoleToJointPermittedTriggers.CheckEligibility);
            UpdateProcessRequestObject.Documents.Add(Guid.Empty);
        }
    }
}
