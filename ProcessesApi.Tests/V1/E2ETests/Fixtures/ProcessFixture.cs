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

        private void createProcess()
        {
            var process = _fixture.Build<Process>()
                        .With(x => x.ProcessName, ProcessNamesConstants.SoleToJoint)
                        .With(x => x.CurrentState,
                                _fixture.Build<ProcessState>()
                                        .With(x => x.State, SoleToJointStates.SelectTenants)
                                        .Create())
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();
            Process = process;
            ProcessId = process.Id.ToString();
            ProcessName = process.ProcessName;
            IncomingTenantId = Guid.NewGuid();
            PersonTenures = _fixture.CreateMany<Guid>().ToList();
        }

        public async Task GivenASoleToJointProcessExists()
        {
            createProcess();
            await _dbFixture.SaveEntityAsync<ProcessesDb>(Process.ToDatabase()).ConfigureAwait(false);
        }

        public void GivenASoleToJointProcessDoesNotExist()
        {
            createProcess();
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

        public void GivenANewSoleToJointProcessRequestWithANotSupportedProcess()
        {
            CreateProcessRequest = _fixture.Create<CreateProcess>();
            ProcessName = "unsupported process";

        }

        public void AndGivenAnUpdateSoleToJointProcessRequest()
        {
            UpdateProcessRequest = new UpdateProcessQuery
            {
                Id = Process.Id,
                ProcessName = Process.ProcessName,
                ProcessTrigger = SoleToJointPermittedTriggers.CheckEligibility
            };
            UpdateProcessRequestObject = _fixture.Create<UpdateProcessQueryObject>();
            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.IncomingTenantId, IncomingTenantId);
        }
        public void AndGivenAnUpdateSoleToJointProcessRequestWithValidationErrors()
        {
            AndGivenAnUpdateSoleToJointProcessRequest();
            UpdateProcessRequestObject.Documents.Add(Guid.Empty);
        }

        public void AndGivenAnUpdateSoleToJointProcessRequestWithsAnUnSupportedProcess()
        {
            AndGivenAnUpdateSoleToJointProcessRequest();
            UpdateProcessRequest.ProcessName = "unsupported process";
        }
    }
}
