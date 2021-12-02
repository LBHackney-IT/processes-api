using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Domain.Enums;
using ProcessesApi.V1.Domain.SoleToJoint;
using ProcessesApi.V1.UseCase.Interfaces;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class SoleToJointService : ISoleToJointService
    {
        private StateMachine<SoleToJointStates, SoleToJointTriggers> _machine;
        private ProcessState<SoleToJointStates, SoleToJointTriggers> _currentState;
        private SoleToJointProcess _soleToJointProcess;

        public SoleToJointService()
        {
        }

        private void SetUpStates()
        {
            _machine.Configure(SoleToJointStates.InitialiseProcess)
                .Permit(SoleToJointTriggers.StartApplication, SoleToJointStates.SelectTenants);
            _machine.Configure(SoleToJointStates.SelectTenants)
                .PermitIf(SoleToJointTriggers.CheckEligibility, SoleToJointStates.AutomatedChecksFailed, () => !_soleToJointProcess.IsEligible())
                .PermitIf(SoleToJointTriggers.CheckEligibility, SoleToJointStates.AutomatedChecksPassed, () => _soleToJointProcess.IsEligible());

        }

        private void SetUpStateActions()
        {
            Configure(SoleToJointStates.SelectTenants, Assignment.Create("tenants"));

        }

        private void Configure(SoleToJointStates state, Assignment assignment)
        {
            _machine.Configure(state)
                .OnEntry((x) =>
                {
                    var processRequest = x.Parameters[0] as SoleToJointTrigger<SoleToJointTriggers>;

                    _currentState = ProcessState<SoleToJointStates, SoleToJointTriggers>.Create(_machine.State, _machine.PermittedTriggers.ToList(), assignment, ProcessData.Create(processRequest.FormData, processRequest.Documents), DateTime.UtcNow, DateTime.UtcNow);
                });
        }

        public async Task Process(SoleToJointTrigger<SoleToJointTriggers> processRequest, SoleToJointProcess soleToJointProcess)
        {
            _soleToJointProcess = soleToJointProcess;

            var state = soleToJointProcess.CurrentState is null ? SoleToJointStates.InitialiseProcess : soleToJointProcess.CurrentState.CurrentStateEnum;

            _machine = new StateMachine<SoleToJointStates, SoleToJointTriggers>(() => state, s => state = s);
            var res = _machine.SetTriggerParameters<SoleToJointTrigger<SoleToJointTriggers>, SoleToJointProcess>(processRequest.Trigger);

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
