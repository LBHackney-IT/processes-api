using ProcessesApi.V1.Domain;
using Stateless;
using System;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class TestService : ProcessService
    {
        public TestService()
        {
            _permittedTriggersConstants = typeof(TestPermittedTriggers);
        }

        private async Task runSomeFunction(StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as UpdateProcessState;
            var randomGenerator = new Random();
            var rand = randomGenerator.Next(0, 100);

            processRequest.Trigger = rand % 2 == 0 ? TestInternalTriggers.ConditionPassed : TestInternalTriggers.ConditionFailed;

            var res = _machine.SetTriggerParameters<UpdateProcessState, Process>(processRequest.Trigger);
            await _machine.FireAsync(res, processRequest, _process);

        }

        protected override void SetUpStates()
        {
            _machine.Configure(ProcessStates.ApplicationInitialised)
                .Permit(ProcessInternalTriggers.StartApplication, TestStates.StateOne);
            _machine.Configure(TestStates.StateOne)
                .Permit(TestPermittedTriggers.FirstTrigger, TestStates.Two);
            _machine.Configure(TestStates.Two)
                .InternalTransitionAsync(TestPermittedTriggers.TriggerConditional, async (x) => await runSomeFunction(x).ConfigureAwait(false))
                .Permit(TestInternalTriggers.ConditionPassed, TestStates.ConditionalThreeA)
                .Permit(TestInternalTriggers.ConditionFailed, TestStates.ConditionalThreeB);
        }

        protected override void SetUpStateActions()
        {
            Configure(TestStates.Two, new Assignment("tenants"), null);
            Configure(TestStates.StateOne, new Assignment("tenants"), null);
            Configure(TestStates.ConditionalThreeA, new Assignment("tenants"), null);
            Configure(TestStates.ConditionalThreeB, new Assignment("tenants"), null);
        }
    }
}
