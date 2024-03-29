using Hackney.Core.JWT;
using Hackney.Core.Sns;
using Hackney.Shared.Processes.Domain;
using Hackney.Shared.Processes.Factories;
using ProcessesApi.V1.Services.Exceptions;
using ProcessesApi.V1.Services.Interfaces;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Hackney.Shared.Processes.Domain.Constants;
using SharedPermittedTriggers = Hackney.Shared.Processes.Domain.Constants.SharedPermittedTriggers;
using Hackney.Shared.Processes.Sns;

namespace ProcessesApi.V1.Services
{
    public class ProcessService : IProcessService
    {
        protected StateMachine<string, string> _machine;
        protected ProcessState _currentState;
        protected Process _process;

        protected Type _permittedTriggersType;

        protected List<string> GetPermittedTriggers()
        {
            var permittedTriggers = _permittedTriggersType?.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                                                          .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
                                                          .Select(x => (string) x.GetRawConstantValue())
                                                          .ToList();
            var sharedTriggers = typeof(SharedPermittedTriggers).GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                                                               .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
                                                               .Select(x => (string) x.GetRawConstantValue());
            permittedTriggers.AddRange(sharedTriggers);

            return permittedTriggers;
        }

        protected List<string> _ignoredTriggersForProcessUpdated;
        protected Dictionary<string, object> _eventData;
        protected ISnsGateway _snsGateway;
        protected Token _token;

        public ProcessService(ISnsGateway snsGateway)
        {
            _snsGateway = snsGateway;
        }

        protected async Task TransitionState(StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as ProcessTrigger;
            var assignment = Assignment.Create("tenants"); // placeholder
            var permittedTriggers = GetPermittedTriggers();

            _currentState = ProcessState.Create(
                _machine.State,
                _machine.PermittedTriggers.Where(trigger => permittedTriggers.Contains(trigger)).ToList(),
                assignment,
                ProcessData.Create(processRequest.FormData, processRequest.Documents),
                DateTime.UtcNow,
                DateTime.UtcNow
            );
            await _process.AddState(_currentState);

            await PublishProcessUpdatedEvent(x).ConfigureAwait(false);
        }

        private void ConfigureStateTransitions()
        {
            _machine.OnTransitionCompletedAsync(async x =>
            {
                await TransitionState(x).ConfigureAwait(false);
            });
        }

        protected async Task PublishProcessStartedEvent(StateMachine<string, string>.Transition x, string additionalEvent = null)
        {
            await TransitionState(x).ConfigureAwait(false);
            var processStartedSnsMessage = _process.CreateProcessStartedEvent(_token);

            var processTopicArn = Environment.GetEnvironmentVariable("PROCESS_SNS_ARN");
            await _snsGateway.Publish(processStartedSnsMessage, processTopicArn).ConfigureAwait(false);

            if (additionalEvent is null) return;

            var processEntityMessage = _process.CreateProcessStartedAgainstEntityEvent(_token, additionalEvent);
            await _snsGateway.Publish(processEntityMessage, processTopicArn).ConfigureAwait(false);
        }

        protected async Task PublishProcessUpdatedEvent(StateMachine<string, string>.Transition transition)
        {
            if (!_ignoredTriggersForProcessUpdated.Contains(transition.Trigger))
            {
                var processTopicArn = Environment.GetEnvironmentVariable("PROCESS_SNS_ARN");
                var processSnsMessage = transition.CreateProcessStateUpdatedEvent(_currentState.CreatedAt, _eventData, EventConstants.PROCESS_UPDATED_EVENT, _token);

                await _snsGateway.Publish(processSnsMessage, processTopicArn).ConfigureAwait(false);
            }
        }

        protected async Task PublishProcessClosedEvent(StateMachine<string, string>.Transition transition)
        {
            var processTopicArn = Environment.GetEnvironmentVariable("PROCESS_SNS_ARN");
            var processSnsMessage = transition.CreateProcessStateUpdatedEvent(DateTime.UtcNow, _eventData, EventConstants.PROCESS_CLOSED_EVENT, _token);

            await _snsGateway.Publish(processSnsMessage, processTopicArn).ConfigureAwait(false);
        }

        protected async Task PublishProcessCompletedEvent(StateMachine<string, string>.Transition transition)
        {
            var processTopicArn = Environment.GetEnvironmentVariable("PROCESS_SNS_ARN");
            var processSnsMessage = transition.CreateProcessStateUpdatedEvent(DateTime.UtcNow, _eventData, EventConstants.PROCESS_COMPLETED_EVENT, _token);

            await _snsGateway.Publish(processSnsMessage, processTopicArn).ConfigureAwait(false);
        }

        public async Task TriggerStateMachine(ProcessTrigger trigger)
        {
            var res = _machine.SetTriggerParameters<ProcessTrigger, Process>(trigger.Trigger);
            await _machine.FireAsync(res, trigger, _process).ConfigureAwait(false);
        }

        protected virtual void SetUpStates()
        {
            // All services must implement the following lines to allow the first state to be initialised correctly:
            // _machine.Configure(SharedStates.ApplicationInitialised)
            //         .Permit(SharedPermittedTriggers.StartApplication, SOME_STATE);
        }

        public async Task Process(ProcessTrigger processRequest, Process process, Token token)
        {
            _process = process;
            _token = token;

            var state = process.CurrentState is null ? SharedStates.ApplicationInitialised : process.CurrentState.State;

            _machine = new StateMachine<string, string>(() => state, s => state = s);
            var res = _machine.SetTriggerParameters<ProcessTrigger, Process>(processRequest.Trigger);

            ConfigureStateTransitions();
            SetUpStates();

            var triggerIsPermitted = GetPermittedTriggers().Contains(processRequest.Trigger);
            var canFire = triggerIsPermitted && _machine.CanFire(processRequest.Trigger);

            if (!canFire)
                throw new InvalidTriggerException(processRequest.Trigger, _machine.State);

            await _machine.FireAsync(res, processRequest, process).ConfigureAwait(false);
        }
    }
}
