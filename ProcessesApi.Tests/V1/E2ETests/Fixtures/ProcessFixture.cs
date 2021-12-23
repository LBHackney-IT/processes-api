using System;
using System.Linq;
using System.Threading.Tasks;
using Amazon.DynamoDBv2.DataModel;
using AutoFixture;
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
        public readonly IDynamoDBContext _dbContext;

        public Process Process { get; private set; }
        public string ProcessId { get; private set; }
        public string ProcessName { get; private set; }
        public string InvalidProcessId { get; private set; }
        public CreateProcess CreateProcessRequest { get; private set; }
        public UpdateProcessQuery UpdateProcessRequest { get; private set; }
        public UpdateProcessQueryObject UpdateProcessRequestObject { get; private set; }
        public Guid IncomingTenantId { get; private set; }

        public ProcessFixture(IDynamoDBContext context)
        {
            _dbContext = context;
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

        public async Task GivenASoleToJointProcessExists()
        {
            var process = _fixture.Build<Process>()
                        .With(x => x.ProcessName, ProcessNamesConstants.SoleToJoint)
                        .With(x => x.CurrentState,
                                _fixture.Build<ProcessState>()
                                        .With(x => x.State, SoleToJointStates.ApplicationInitialised)
                                        .With(x => x.PermittedTriggers, (new[] { SoleToJointInternalTriggers.StartApplication }).ToList())
                                        .Create())
                        .Without(x => x.PreviousStates)
                        .With(x => x.VersionNumber, (int?) null)
                        .Create();
            await _dbContext.SaveAsync<ProcessesDb>(process.ToDatabase()).ConfigureAwait(false);

            Process = process;
            ProcessId = process.Id.ToString();
            ProcessName = process.ProcessName;
            IncomingTenantId = Guid.NewGuid();
        }

        public void GivenASoleToJointProcessDoesNotExist()
        {
        }

        public async Task GivenAnInvalidProcessId()
        {
            var process = _fixture.Build<Process>()
            .With(x => x.ProcessName, ProcessNamesConstants.SoleToJoint)
            .With(x => x.CurrentState,
                    _fixture.Build<ProcessState>()
                            .With(x => x.State, SoleToJointStates.ApplicationInitialised)
                            .With(x => x.PermittedTriggers, (new[] { SoleToJointInternalTriggers.StartApplication }).ToList())
                            .Create())
            .Without(x => x.PreviousStates)
            .With(x => x.VersionNumber, (int?) null)
            .Create();
            await _dbContext.SaveAsync<ProcessesDb>(process.ToDatabase()).ConfigureAwait(false);
            ProcessId = process.Id.ToString();
            ProcessName = process.ProcessName;
            InvalidProcessId = "abcdefg";
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

        public void GivenAnUpdateSoleToJointProcessRequest()
        {
            UpdateProcessRequest = _fixture.Create<UpdateProcessQuery>();
            UpdateProcessRequestObject = _fixture.Create<UpdateProcessQueryObject>();
            UpdateProcessRequestObject.FormData.Add(SoleToJointFormDataKeys.IncomingTenantId, IncomingTenantId);
        }
    }
}
