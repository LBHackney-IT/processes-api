using Hackney.Core.JWT;
using ProcessesApi.V1.Domain;
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
        protected Token _token;

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
                .OnEntry(x =>
                {
                    var processRequest = x.Parameters[0] as UpdateProcessState;
                    SwitchProcessState(assignment, processRequest);

                    func?.Invoke(processRequest);
                });
        }

        protected void ConfigureAsync(string state, Assignment assignment, Func<UpdateProcessState, Task> func)
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

        public async Task Process(UpdateProcessState processRequest, Process process, Token token)
        {
            _process = process;
            _token = token;

            var state = process.CurrentState is null ? SharedProcessStates.ApplicationInitialised : process.CurrentState.State;

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
