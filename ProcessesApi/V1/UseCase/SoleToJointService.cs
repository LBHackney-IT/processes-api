using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase.Exceptions;
using ProcessesApi.V1.UseCase.Interfaces;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using Hackney.Core.JWT;
using Hackney.Core.Sns;
using ProcessesApi.V1.Factories;

namespace ProcessesApi.V1.UseCase
{
    public class SoleToJointService : ISoleToJointService
    {
        private StateMachine<string, string> _machine;
        private ProcessState _currentState;
        private Process _soleToJointProcess;

        private readonly ISoleToJointGateway _soleToJointGateway;
        private readonly ISnsFactory _snsFactory;
        private readonly ISnsGateway _snsGateway;
        private Token _token;

        public SoleToJointService(ISoleToJointGateway gateway, ISnsFactory snsFactory, ISnsGateway snsGateway)
        {
            _soleToJointGateway = gateway;
            _snsFactory = snsFactory;
            _snsGateway = snsGateway;
        }

        private async Task CheckEligibility(StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as UpdateProcessState;
            try
            {
                var isEligible = await _soleToJointGateway.CheckEligibility(_soleToJointProcess.TargetId,
                                                                            Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.IncomingTenantId].ToString()),
                                                                            Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.TenantId].ToString()))
                                            .ConfigureAwait(false);

                processRequest.Trigger = isEligible ? SoleToJointInternalTriggers.EligibiltyPassed : SoleToJointInternalTriggers.EligibiltyFailed;

                var res = _machine.SetTriggerParameters<UpdateProcessState, Process>(processRequest.Trigger);
                await _machine.FireAsync(res, processRequest, _soleToJointProcess);
            }
            catch (KeyNotFoundException)
            {
                var expectedFormDataKeys = new List<string>
                {
                    SoleToJointFormDataKeys.IncomingTenantId,
                    SoleToJointFormDataKeys.TenantId
                };
                throw new FormDataNotFoundException(processRequest.FormData.Keys.ToList(), expectedFormDataKeys);
            }
        }

        private async Task ValidateManualCheck(
            StateMachine<string, string>.Transition transition,
            string passedTrigger, string failedTrigger,
            params (string CheckId, string Value)[] expectations)
        {
            var processRequest = (UpdateProcessState) transition.Parameters[0];
            var formData = processRequest.FormData;

            try
            {
                var isCheckPassed = expectations.All(expectation =>
                    String.Equals(
                        expectation.Value,
                        formData[expectation.CheckId].ToString(),
                        StringComparison.OrdinalIgnoreCase));

                processRequest.Trigger = isCheckPassed
                    ? passedTrigger
                    : failedTrigger;

                var trigger = _machine.SetTriggerParameters<UpdateProcessState, Process>(processRequest.Trigger);
                await _machine.FireAsync(trigger, processRequest, _soleToJointProcess);
            }
            catch (KeyNotFoundException)
            {
                var expectedFormDataKeys = expectations.Select(expectation => expectation.CheckId).ToList();
                throw new FormDataNotFoundException(formData.Keys.ToList(), expectedFormDataKeys);
            }
        }

        private async Task CheckManualEligibility(StateMachine<string, string>.Transition transition)
        {
            await ValidateManualCheck(
                    transition,
                    SoleToJointInternalTriggers.ManualEligibilityPassed,
                    SoleToJointInternalTriggers.ManualEligibilityFailed,
                    (SoleToJointFormDataKeys.BR11, "true"),
                    (SoleToJointFormDataKeys.BR12, "false"),
                    (SoleToJointFormDataKeys.BR13, "false"),
                    (SoleToJointFormDataKeys.BR15, "false"),
                    (SoleToJointFormDataKeys.BR16, "false"))
                .ConfigureAwait(false);
        }

        private async Task CheckTenancyBreach(StateMachine<string, string>.Transition transition)
        {
            await ValidateManualCheck(
                    transition,
                    SoleToJointInternalTriggers.BreachChecksPassed,
                    SoleToJointInternalTriggers.BreachChecksFailed,
                    (SoleToJointFormDataKeys.BR5, "false"),
                    (SoleToJointFormDataKeys.BR10, "false"),
                    (SoleToJointFormDataKeys.BR17, "false"),
                    (SoleToJointFormDataKeys.BR18, "false"))
                .ConfigureAwait(false);
        }

        private void SetUpStates()
        {
            _machine.Configure(SoleToJointStates.ApplicationInitialised)
                    .Permit(SoleToJointInternalTriggers.StartApplication, SoleToJointStates.SelectTenants);
            _machine.Configure(SoleToJointStates.SelectTenants)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckEligibility, async (x) => await CheckEligibility(x).ConfigureAwait(false))
                    .Permit(SoleToJointInternalTriggers.EligibiltyFailed, SoleToJointStates.AutomatedChecksFailed)
                    .Permit(SoleToJointInternalTriggers.EligibiltyPassed, SoleToJointStates.AutomatedChecksPassed);
            _machine.Configure(SoleToJointStates.AutomatedChecksFailed)
                    .Permit(SoleToJointPermittedTriggers.CancelProcess, SoleToJointStates.ProcessCancelled);
            _machine.Configure(SoleToJointStates.AutomatedChecksPassed)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckManualEligibility, async (x) => await CheckManualEligibility(x).ConfigureAwait(false))
                    .Permit(SoleToJointInternalTriggers.ManualEligibilityPassed, SoleToJointStates.ManualChecksPassed)
                    .Permit(SoleToJointInternalTriggers.ManualEligibilityFailed, SoleToJointStates.ManualChecksFailed);
            _machine.Configure(SoleToJointStates.ManualChecksFailed)
                    .Permit(SoleToJointPermittedTriggers.CancelProcess, SoleToJointStates.ProcessCancelled);
            _machine.Configure(SoleToJointStates.ManualChecksPassed)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckTenancyBreach, async (x) => await CheckTenancyBreach(x).ConfigureAwait(false))
                    .Permit(SoleToJointInternalTriggers.BreachChecksPassed, SoleToJointStates.BreachChecksPassed)
                    .Permit(SoleToJointInternalTriggers.BreachChecksFailed, SoleToJointStates.BreachChecksFailed);
            _machine.Configure(SoleToJointStates.BreachChecksFailed)
                    .Permit(SoleToJointPermittedTriggers.CancelProcess, SoleToJointStates.ProcessCancelled);
        }

        private void AddIncomingTenantId(UpdateProcessState processRequest)
        {
            //TODO: When doing a POST request from the FE they should created a relatedEntities object with all neccesary values
            // Once Frontend work is completed the IF statement below should be removed.
            if (_soleToJointProcess.RelatedEntities == null)
                _soleToJointProcess.RelatedEntities = new List<Guid>();
            _soleToJointProcess.RelatedEntities.Add(Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.IncomingTenantId].ToString()));
        }

        private void SetUpStateActions()
        {
            Configure(SoleToJointStates.SelectTenants, Assignment.Create("tenants"), null);
            ConfigureAsync(SoleToJointStates.AutomatedChecksFailed, Assignment.Create("tenants"), OnAutomatedCheckFailed);
            Configure(SoleToJointStates.AutomatedChecksPassed, Assignment.Create("tenants"), AddIncomingTenantId);
            Configure(SoleToJointStates.ProcessCancelled, Assignment.Create("tenants"), null);
            Configure(SoleToJointStates.ManualChecksPassed, Assignment.Create("tenants"), null);
            ConfigureAsync(SoleToJointStates.ManualChecksFailed, Assignment.Create("tenants"), OnManualCheckFailed);
            ConfigureAsync(SoleToJointStates.BreachChecksPassed, Assignment.Create("tenants"), null);
            ConfigureAsync(SoleToJointStates.BreachChecksFailed, Assignment.Create("tenants"), null);
        }

        private async Task OnAutomatedCheckFailed(UpdateProcessState processRequest)
        {
            AddIncomingTenantId(processRequest);

            await PublishProcessClosedEvent("Automatic eligibility check failed - process closed.");
        }

        private async Task OnManualCheckFailed(UpdateProcessState processRequest)
        {
            await PublishProcessClosedEvent("Manual Eligibility Check failed - process closed.");
        }

        private void Configure(string state, Assignment assignment, Action<UpdateProcessState> func)
        {
            _machine.Configure(state)
                .OnEntry(x =>
                {
                    var processRequest = x.Parameters[0] as UpdateProcessState;
                    SwitchProcessState(assignment, processRequest);

                    func?.Invoke(processRequest);
                });
        }

        private void ConfigureAsync(string state, Assignment assignment, Func<UpdateProcessState, Task> func)
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
            var soleToJointPermittedTriggers = typeof(SoleToJointPermittedTriggers)
                .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
                .Select(x => (string) x.GetRawConstantValue())
                .ToList();

            _currentState = ProcessState.Create(
                _machine.State,
                _machine.PermittedTriggers
                    .Where(trigger => soleToJointPermittedTriggers.Contains(trigger)).ToList(),
                assignment,
                ProcessData.Create(processRequest.FormData, processRequest.Documents),
                DateTime.UtcNow, DateTime.UtcNow);
        }

        private async Task PublishProcessClosedEvent(string description)
        {
            var processTopicArn = Environment.GetEnvironmentVariable("PROCESS_SNS_ARN");
            var processSnsMessage = _snsFactory.ProcessClosed(_soleToJointProcess, _token, description);

            await _snsGateway.Publish(processSnsMessage, processTopicArn).ConfigureAwait(false);
        }

        public async Task Process(UpdateProcessState processRequest, Process soleToJointProcess, Token token)
        {
            _soleToJointProcess = soleToJointProcess;
            _token = token;

            var state = soleToJointProcess.CurrentState is null ? SoleToJointStates.ApplicationInitialised : soleToJointProcess.CurrentState.State;

            _machine = new StateMachine<string, string>(() => state, s => state = s);
            var res = _machine.SetTriggerParameters<UpdateProcessState, Process>(processRequest.Trigger);

            SetUpStates();
            SetUpStateActions();

            var canFire = _machine.CanFire(processRequest.Trigger);

            if (!canFire)
                throw new Exception($"Cannot trigger {processRequest.Trigger} from {_machine.State}");

            await _machine.FireAsync(res, processRequest, soleToJointProcess);

            await soleToJointProcess.AddState(_currentState);
        }
    }
}
