using ProcessesApi.V1.Domain;
using ProcessesApi.V1.UseCase.Interfaces;
using Stateless;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class ProcessService : IProcessService
    {
        protected StateMachine<string, string> _machine;
        protected ProcessState _currentState;
        protected Process _process;
        protected Type _permittedTriggersConstants;
        protected List<string> _permittedTriggers => _permittedTriggersConstants
                                                    .GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy)
                                                    .Where(fi => fi.IsLiteral && !fi.IsInitOnly && fi.FieldType == typeof(string))
                                                    .Select(x => (string) x.GetRawConstantValue())
                                                    .ToList();

        public ProcessService()
        {
        }

        protected virtual void SetUpStates()
        {
        }

        protected virtual void SetUpStateActions()
        {
        }

        protected void Configure(string state, Assignment assignment, Action<UpdateProcessState> func)
        {
            _machine.Configure(state)
                .OnEntry((x) =>
                {
                    var processRequest = x.Parameters[0] as UpdateProcessState;

                    _currentState = ProcessState.Create(_machine.State, _machine.PermittedTriggers.Where(x => _permittedTriggers.Contains(x)).ToList(), assignment, ProcessData.Create(processRequest.FormData, processRequest.Documents), DateTime.UtcNow, DateTime.UtcNow);
                    func?.Invoke(processRequest);
                });
        }

        public async Task Process(UpdateProcessState processRequest, Process process)
        {
            _process = process;

            var state = process.CurrentState is null ? ProcessStates.ApplicationInitialised : process.CurrentState.State;

            _machine = new StateMachine<string, string>(() => state, s => state = s);
            var res = _machine.SetTriggerParameters<UpdateProcessState, Process>(processRequest.Trigger);

            SetUpStates();
            SetUpStateActions();

            var canFire = _machine.CanFire(processRequest.Trigger);

            if (!canFire)
                throw new Exception($"Cannot trigger {processRequest.Trigger} from {_machine.State}");

            await _machine.FireAsync(res, processRequest, process);

            await process.AddState(_currentState);
        }
    }
}
