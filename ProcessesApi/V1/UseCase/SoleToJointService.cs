using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Gateways;
using ProcessesApi.V1.UseCase.Interfaces;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class SoleToJointService : ISoleToJointService
    {
        private StateMachine<string, string> _machine;
        private ProcessState _currentState;
        private Process _soleToJointProcess;
        private ISoleToJointGateway _soleToJointGateway;

        public SoleToJointService(ISoleToJointGateway gateway)
        {
            _soleToJointGateway = gateway;
        }

        private async Task CheckEligibility(StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as UpdateProcessState;
            var isEligible = await _soleToJointGateway.CheckEligibility(_soleToJointProcess.TargetId,
                                                                        Guid.Parse(processRequest.FormData["incomingTenantId"].ToString()))
                                        .ConfigureAwait(false);

            processRequest.Trigger = isEligible ? SoleToJointInternalTriggers.EligibiltyPassed : SoleToJointInternalTriggers.EligibiltyFailed;

            var res = _machine.SetTriggerParameters<UpdateProcessState, Process>(processRequest.Trigger);
            await _machine.FireAsync(res, processRequest, _soleToJointProcess);
        }

        private async Task CheckManualEligibility(StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as UpdateProcessState;
            var eligibilityFormData = processRequest.FormData;

            var isEligible = eligibilityFormData[SoleToJointFormDataKeys.BR11].ToString() == "true"
                             && eligibilityFormData[SoleToJointFormDataKeys.BR12].ToString() == "false"
                             && eligibilityFormData[SoleToJointFormDataKeys.BR13].ToString() == "false"
                             && eligibilityFormData[SoleToJointFormDataKeys.BR15].ToString() == "false"
                             && eligibilityFormData[SoleToJointFormDataKeys.BR16].ToString() == "false";

            processRequest.Trigger = isEligible ? SoleToJointInternalTriggers.ManualEligibilityPassed : SoleToJointInternalTriggers.ManualEligibilityFailed;

            var res = _machine.SetTriggerParameters<UpdateProcessState, Process>(processRequest.Trigger);
            await _machine.FireAsync(res, processRequest, _soleToJointProcess);
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
                    .Permit(SoleToJointPermittedTriggers.CancelProcess, SoleToJointStates.ProcessClosed);
            _machine.Configure(SoleToJointStates.AutomatedChecksPassed)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckManualEligibility, async (x) => await CheckManualEligibility(x).ConfigureAwait(false))
                    .Permit(SoleToJointInternalTriggers.ManualEligibilityPassed, SoleToJointStates.ManualChecksPassed)
                    .Permit(SoleToJointInternalTriggers.ManualEligibilityFailed, SoleToJointStates.ManualChecksFailed);
            _machine.Configure(SoleToJointStates.ManualChecksFailed)
                    .Permit(SoleToJointPermittedTriggers.CancelProcess, SoleToJointStates.ProcessClosed);
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
            Configure(SoleToJointStates.AutomatedChecksFailed, Assignment.Create("tenants"), AddIncomingTenantId);
            Configure(SoleToJointStates.AutomatedChecksPassed, Assignment.Create("tenants"), AddIncomingTenantId);
            Configure(SoleToJointStates.ProcessClosed, Assignment.Create("tenants"), null);
            Configure(SoleToJointStates.ManualChecksPassed, Assignment.Create("tenants"), null);
            Configure(SoleToJointStates.ManualChecksFailed, Assignment.Create("tenants"), null);
        }

        private void Configure(string state, Assignment assignment, Action<UpdateProcessState> func)
        {
            _machine.Configure(state)
                .OnEntry((x) =>
                {
                    var processRequest = x.Parameters[0] as UpdateProcessState;
                    var soleToJointPermittedTriggers = typeof(SoleToJointPermittedTriggers)
                            .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                            .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
                            .Select(x => (string) x.GetRawConstantValue())
                            .ToList();

                    _currentState = ProcessState.Create(_machine.State, _machine.PermittedTriggers.Where(x => soleToJointPermittedTriggers.Contains(x)).ToList(), assignment, ProcessData.Create(processRequest.FormData, processRequest.Documents), DateTime.UtcNow, DateTime.UtcNow);
                    func?.Invoke(processRequest);
                });
        }

        public async Task Process(UpdateProcessState processRequest, Process soleToJointProcess)
        {
            _soleToJointProcess = soleToJointProcess;

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
