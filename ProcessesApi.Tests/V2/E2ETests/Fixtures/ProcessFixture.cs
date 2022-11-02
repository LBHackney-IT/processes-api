using System;
using System.Collections.Generic;
using Amazon.DynamoDBv2.DataModel;
using Amazon.SimpleNotificationService;
using AutoFixture;
using Hackney.Shared.Processes.Domain;
using Hackney.Shared.Processes.Infrastructure;
using Hackney.Shared.Processes.Boundary.Request.V2;

namespace ProcessesApi.Tests.V2.E2E.Fixtures
{
    public class ProcessFixture : IDisposable
    {
        public readonly Fixture _fixture = new Fixture();
        public readonly IDynamoDBContext _dbContext;
        private readonly IAmazonSimpleNotificationService _amazonSimpleNotificationService;
        public Process Process { get; private set; }
        public ProcessName ProcessName { get; private set; }
        public CreateProcess CreateProcessRequest { get; private set; }

        public ProcessFixture(IDynamoDBContext context, IAmazonSimpleNotificationService amazonSimpleNotificationService)
        {
            _dbContext = context;
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
            ProcessName = process.ProcessName;
        }

        private void GivenANewProcessRequest()
        {
            var targetId = Guid.NewGuid();
            var targetType = _fixture.Create<TargetType>();
            var relatedEntities = new List<RelatedEntity>
            {
                _fixture.Build<RelatedEntity>().With(x => x.TargetType, TargetType.asset).Create(),
                _fixture.Build<RelatedEntity>().With(x => x.TargetType, TargetType.person).Create(),
                _fixture.Build<RelatedEntity>().With(x => x.TargetType, TargetType.tenure).Create(),
                _fixture.Build<RelatedEntity>().With(x => x.TargetType, TargetType.tenure).With(x => x.Id, targetId).Create()
            };

            CreateProcessRequest = _fixture.Build<CreateProcess>()
                                           .With(x => x.TargetId, targetId)
                                           .With(x => x.TargetType, targetType)
                                           .With(x => x.RelatedEntities, relatedEntities)
                                           .Create();
        }

        public void GivenANewSoleToJointProcessRequest()
        {
            GivenANewProcessRequest();
            ProcessName = ProcessName.soletojoint;
        }

        public void GivenANewChangeOfNameProcessRequest()
        {
            GivenANewProcessRequest();
            ProcessName = ProcessName.changeofname;
        }

        public void GivenANewSoleToJointProcessRequestWithValidationErrors()
        {
            CreateProcessRequest = _fixture.Build<CreateProcess>()
                            .With(x => x.TargetId, Guid.Empty)
                            .Create();
            ProcessName = ProcessName.soletojoint;
        }
    }
}
