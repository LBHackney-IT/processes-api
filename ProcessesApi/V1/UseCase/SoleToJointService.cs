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
            _machine.Configure(SoleToJointStates.ApplicationStarted).Permit(SoleToJointTriggers.StartApplication, SoleToJointStates.SelectTenants);
            _machine.Configure(SoleToJointStates.SelectTenants).Permit(SoleToJointTriggers.CheckEligibility, SoleToJointStates.SelectTenants);

            _machine.Configure(SoleToJointStates.CheckEligibility)
                .PermitIf(SoleToJointTriggers.CheckEligibility, SoleToJointStates.AutomatedChecksFailed, () => !_soleToJointProcess.IsEligible())
                .PermitIf(SoleToJointTriggers.CheckEligibility, SoleToJointStates.AutomatedChecksPassed, () => !_soleToJointProcess.IsEligible());
            _machine.Configure(SoleToJointStates.AutomatedChecksFailed)
                .Permit(SoleToJointTriggers.ExitApplication, SoleToJointStates.AutomatedChecksFailed);
            _machine.Configure(SoleToJointStates.AutomatedChecksPassed)
                .Permit(SoleToJointTriggers.CheckManualEligibility, SoleToJointStates.AutomatedChecksPassed);

        }

        private void SetUpStateActions()
        {
            Configure(SoleToJointStates.ApplicationStarted, Assignment.Create("tenants"));

            _machine.Configure(SoleToJointStates.CheckEligibility)
                .OnEntryAsync((x) =>
                {
                    var processRequest = x.Parameters[0] as SoleToJointObject<SoleToJointTriggers>;
                    var soleToJointProcess = x.Parameters[1] as SoleToJointProcess;

                    _currentState = ProcessState<SoleToJointStates, SoleToJointTriggers>.Create(_machine.State, _machine.PermittedTriggers.ToList(), Assignment.Create("tenants"), processRequest?.ProcessRequest);

                    var application = soleToJointProcess?.ProcessStates.First(x => x.CurrentStateEnum == SoleToJointStates.ApplicationStarted);

                    var formData = JsonSerializer.Deserialize<SoleToJointFormData>(application?.ProcessData.FormData, new JsonSerializerOptions() { PropertyNameCaseInsensitive = true });

                    return Task.CompletedTask;
                });

            Configure(SoleToJointStates.AutomatedChecksFailed, Assignment.Create("tenants"));
            Configure(SoleToJointStates.AutomatedChecksPassed, Assignment.Create("tenants"));
           
        }

        private void Configure(SoleToJointStates state, Assignment assignment)
        {
            _machine.Configure(state)
                .OnEntry((x) =>
                {
                    var processRequest = x.Parameters[0] as SoleToJointObject<SoleToJointTriggers>;

                    _currentState = ProcessState<SoleToJointStates, SoleToJointTriggers>.Create(_machine.State, _machine.PermittedTriggers.ToList(), assignment, processRequest?.ProcessRequest);
                });
        }

        public async Task Process(SoleToJointObject<SoleToJointTriggers> processRequest, SoleToJointProcess soleToJointProcess)
        {
            _soleToJointProcess = soleToJointProcess;

            var state = soleToJointProcess.ProcessStates.Count > 0 ? soleToJointProcess.ProcessStates.Last().CurrentStateEnum : SoleToJointStates.ApplicationStarted;

            _machine = new StateMachine<SoleToJointStates, SoleToJointTriggers>(() => state, s => state = s);
            var res = _machine.SetTriggerParameters<SoleToJointObject<SoleToJointTriggers>, SoleToJointProcess>(processRequest.Trigger);

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
