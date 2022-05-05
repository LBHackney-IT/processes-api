using Hackney.Core.JWT;
using Hackney.Core.Sns;
using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Services.Exceptions;
using ProcessesApi.V1.Services.Interfaces;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ProcessesApi.V1.Services
{
    public class ProcessService : IProcessService
    {
        protected StateMachine<string, string> _machine;
        protected ProcessState _currentState;
        protected Process _process;
        protected Type _permittedTriggersType;
        protected List<string> _permittedTriggers => _permittedTriggersType
                                                    .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                                                    .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
                                                    .Select(x => (string) x.GetRawConstantValue())
                                                    .ToList();
        protected ISnsFactory _snsFactory;
        protected ISnsGateway _snsGateway;
        protected Token _token;

        public ProcessService(ISnsFactory snsFactory, ISnsGateway snsGateway)
        {
            _snsFactory = snsFactory;
            _snsGateway = snsGateway;
        }

        protected virtual void SetUpStates()
        {
            // All services must implement the following lines to allow the first state to be initialised correctly:
            // _machine.Configure(SharedProcessStates.ApplicationInitialised)
            //         .Permit(SharedInternalTriggers.StartApplication, SOME_STATE);
        }

        protected virtual void SetUpStateActions()
        {
        }

        protected void Configure(string state, Assignment assignment, Action<UpdateProcessState> func = null)
        {
            _machine.Configure(state)
                .OnEntry(x =>
                {
                    var processRequest = x.Parameters[0] as UpdateProcessState;
                    SwitchProcessState(assignment, processRequest);

                    func?.Invoke(processRequest);
                });
        }

        protected void ConfigureAsync(string state, Assignment assignment, Func<UpdateProcessState, Task> func = null)
        {
            _machine.Configure(state)
                .OnEntryAsync(async x =>
                {
                    var processRequest = x.Parameters[0] as UpdateProcessState;
                    SwitchProcessState(assignment, processRequest);

                    if (func != null)
                    {
                        await func.Invoke(processRequest).ConfigureAwait(false);
                    }
                });
        }

        private void SwitchProcessState(Assignment assignment, UpdateProcessState processRequest)
        {
            _currentState = ProcessState.Create(
                _machine.State,
                _machine.PermittedTriggers
                    .Where(trigger => _permittedTriggers.Contains(trigger)).ToList(),
                assignment,
                ProcessData.Create(processRequest.FormData, processRequest.Documents),
                DateTime.UtcNow, DateTime.UtcNow);
        }

        protected async Task TriggerStateMachine(UpdateProcessState trigger)
        {
            var res = _machine.SetTriggerParameters<UpdateProcessState, Process>(trigger.Trigger);
            await _machine.FireAsync(res, trigger, _process).ConfigureAwait(false);
        }

        protected async Task PublishProcessStartedEvent()
        {
            var processTopicArn = Environment.GetEnvironmentVariable("PROCESS_SNS_ARN");
            var processSnsMessage = _snsFactory.ProcessStarted(_process, _token);

            await _snsGateway.Publish(processSnsMessage, processTopicArn).ConfigureAwait(false);
        }

        protected async Task PublishProcessUpdatedEvent(string description)
        {
            var processTopicArn = Environment.GetEnvironmentVariable("PROCESS_SNS_ARN");
            var processSnsMessage = _snsFactory.ProcessUpdatedWithMessage(_process, _token, description);

            await _snsGateway.Publish(processSnsMessage, processTopicArn).ConfigureAwait(false);
        }

        protected async Task PublishProcessClosedEvent(string description)
        {
            var processTopicArn = Environment.GetEnvironmentVariable("PROCESS_SNS_ARN");
            var processSnsMessage = _snsFactory.ProcessClosed(_process, _token, description);

            await _snsGateway.Publish(processSnsMessage, processTopicArn).ConfigureAwait(false);
        }

        public async Task Process(UpdateProcessState processRequest, Process process, Token token)
        {
            _process = process;
            _token = token;

            var state = process.CurrentState is null ? SharedProcessStates.ApplicationInitialised : process.CurrentState.State;

            _machine = new StateMachine<string, string>(() => state, s => state = s);
            var res = _machine.SetTriggerParameters<UpdateProcessState, Process>(processRequest.Trigger);

            SetUpStates();
            SetUpStateActions();

            var triggerIsPermitted = _permittedTriggers.Contains(processRequest.Trigger) || processRequest.Trigger == SharedInternalTriggers.StartApplication;
            var canFire = triggerIsPermitted && _machine.CanFire(processRequest.Trigger);

            if (!canFire)
                throw new InvalidTriggerException(processRequest.Trigger, _machine.State);

            await _machine.FireAsync(res, processRequest, process).ConfigureAwait(false);

            await process.AddState(_currentState);
        }
    }
}
