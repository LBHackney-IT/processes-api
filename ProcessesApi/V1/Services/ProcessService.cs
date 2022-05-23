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
        protected List<string> _ignoredTriggersForProcessUpdated;
        protected Dictionary<string, object> _eventData;

        protected ISnsFactory _snsFactory;
        protected ISnsGateway _snsGateway;
        protected Token _token;

        public ProcessService(ISnsFactory snsFactory, ISnsGateway snsGateway)
        {
            _snsFactory = snsFactory;
            _snsGateway = snsGateway;
        }

        private void ConfigureStateTransitions()
        {
            _machine.OnTransitionCompletedAsync(async x =>
            {
                var processRequest = x.Parameters[0] as ProcessTrigger;
                var assignment = Assignment.Create("tenants"); // placeholder

                await PublishProcessUpdatedEvent(x).ConfigureAwait(false);

                _currentState = ProcessState.Create(
                    _machine.State,
                    _machine.PermittedTriggers
                        .Where(trigger => _permittedTriggers.Contains(trigger)).ToList(),
                    assignment,
                    ProcessData.Create(processRequest.FormData, processRequest.Documents),
                    DateTime.UtcNow, DateTime.UtcNow
                );
            });
        }

        protected async Task PublishProcessStartedEvent()
        {
            var processTopicArn = Environment.GetEnvironmentVariable("PROCESS_SNS_ARN");
            var processSnsMessage = _snsFactory.ProcessStarted(_process, _token);

            await _snsGateway.Publish(processSnsMessage, processTopicArn).ConfigureAwait(false);
        }

        protected async Task PublishProcessUpdatedEvent(StateMachine<string, string>.Transition transition)
        {
            if (!_ignoredTriggersForProcessUpdated.Contains(transition.Trigger))
            {
                var processTopicArn = Environment.GetEnvironmentVariable("PROCESS_SNS_ARN");
                var processSnsMessage = _snsFactory.ProcessStateUpdated(transition, _eventData, _token);

                await _snsGateway.Publish(processSnsMessage, processTopicArn).ConfigureAwait(false);
            }
        }

        protected async Task PublishProcessClosedEvent()
        {
            var processTopicArn = Environment.GetEnvironmentVariable("PROCESS_SNS_ARN");
            var processSnsMessage = _snsFactory.ProcessClosed(_process, _token, "Process Closed. The resident has been notified.");

            await _snsGateway.Publish(processSnsMessage, processTopicArn).ConfigureAwait(false);
        }

        protected async Task TriggerStateMachine(ProcessTrigger trigger)
        {
            var res = _machine.SetTriggerParameters<ProcessTrigger, Process>(trigger.Trigger);
            await _machine.FireAsync(res, trigger, _process).ConfigureAwait(false);
        }

        protected virtual void SetUpStates()
        {
            // All services must implement the following lines to allow the first state to be initialised correctly:
            // _machine.Configure(SharedProcessStates.ApplicationInitialised)
            //         .Permit(SharedInternalTriggers.StartApplication, SOME_STATE);
        }

        public async Task Process(ProcessTrigger processRequest, Process process, Token token)
        {
            _process = process;
            _token = token;

            var state = process.CurrentState is null ? SharedProcessStates.ApplicationInitialised : process.CurrentState.State;

            _machine = new StateMachine<string, string>(() => state, s => state = s);
            var res = _machine.SetTriggerParameters<ProcessTrigger, Process>(processRequest.Trigger);

            ConfigureStateTransitions();
            SetUpStates();

            var triggerIsPermitted = _permittedTriggers.Contains(processRequest.Trigger) || processRequest.Trigger == SharedInternalTriggers.StartApplication;
            var canFire = triggerIsPermitted && _machine.CanFire(processRequest.Trigger);

            if (!canFire)
                throw new InvalidTriggerException(processRequest.Trigger, _machine.State);

            await _machine.FireAsync(res, processRequest, process).ConfigureAwait(false);

            await process.AddState(_currentState);
        }
    }
}
