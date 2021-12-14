using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Helper;
using ProcessesApi.V1.UseCase.Interfaces;
using Stateless;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class SoleToJointService : ISoleToJointService
    {
        private StateMachine<string, string> _machine;
        private ProcessState _currentState;
        private Process _soleToJointProcess;

        public SoleToJointService()
        {
        }
        private void SetUpStates()
        {
            _machine.Configure(SoleToJointStates.ApplicationInitialised)
                .Permit(SoleToJointTriggers.StartApplication, SoleToJointStates.SelectTenants);
            _machine.Configure(SoleToJointStates.SelectTenants)
                .PermitIf(SoleToJointTriggers.CheckEligibility, SoleToJointStates.AutomatedChecksFailed, () => !_soleToJointProcess.IsEligible())
                .PermitIf(SoleToJointTriggers.CheckEligibility, SoleToJointStates.AutomatedChecksPassed, () => _soleToJointProcess.IsEligible());

        }

        private void AddIncomingTenantId(UpdateProcessState processRequest)
        {
            _soleToJointProcess.RelatedEntities.Add(Guid.Parse(processRequest.FormData["incomingTenantId"].ToString()));
        }

        private void SetUpStateActions()
        {
            Configure(SoleToJointStates.SelectTenants, Assignment.Create("tenants"), null);
            Configure(SoleToJointStates.AutomatedChecksFailed, Assignment.Create("tenants"), AddIncomingTenantId);
            Configure(SoleToJointStates.AutomatedChecksPassed, Assignment.Create("tenants"), AddIncomingTenantId);
        }

        private void Configure(string state, Assignment assignment, Action<UpdateProcessState> func)
        {
            _machine.Configure(state)
                .OnEntry((x) =>
                {
                    var processRequest = x.Parameters[0] as UpdateProcessState;
                    _currentState = ProcessState.Create(_machine.State, _machine.PermittedTriggers.ToList(), assignment, ProcessData.Create(processRequest.FormData, processRequest.Documents), DateTime.UtcNow, DateTime.UtcNow);
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
