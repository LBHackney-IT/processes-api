using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Gateways;
using Stateless;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace ProcessesApi.V1.UseCase
{
    public class SoleToJointService : ProcessService
    {
        private ISoleToJointGateway _soleToJointGateway;

        public SoleToJointService(ISoleToJointGateway gateway)
        {
            _soleToJointGateway = gateway;
            _permittedTriggersConstants = typeof(SoleToJointPermittedTriggers);
        }

        private async Task CheckEligibility(StateMachine<string, string>.Transition x)
        {
            var processRequest = x.Parameters[0] as UpdateProcessState;
            var isEligible = await _soleToJointGateway.CheckEligibility(_process.TargetId,
                                                                        Guid.Parse(processRequest.FormData["incomingTenantId"].ToString()))
                                        .ConfigureAwait(false);

            processRequest.Trigger = isEligible ? SoleToJointInternalTriggers.EligibiltyPassed : SoleToJointInternalTriggers.EligibiltyFailed;

            var res = _machine.SetTriggerParameters<UpdateProcessState, Process>(processRequest.Trigger);
            await _machine.FireAsync(res, processRequest, _process);
        }

        private void AddIncomingTenantId(UpdateProcessState processRequest)
        {
            //TODO: When doing a POST request from the FE they should created a relatedEntities object with all neccesary values
            // Once Frontend work is completed the IF statement below should be removed.
            if (_process.RelatedEntities == null)
                _process.RelatedEntities = new List<Guid>();
            _process.RelatedEntities.Add(Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.IncomingTenantId].ToString()));
        }

        protected override void SetUpStates()
        {
            _machine.Configure(ProcessStates.ApplicationInitialised)
                .Permit(ProcessInternalTriggers.StartApplication, SoleToJointStates.SelectTenants);
            _machine.Configure(SoleToJointStates.SelectTenants)
                .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckEligibility, async (x) => await CheckEligibility(x).ConfigureAwait(false))
                .Permit(SoleToJointInternalTriggers.EligibiltyFailed, SoleToJointStates.AutomatedChecksFailed)
                .Permit(SoleToJointInternalTriggers.EligibiltyPassed, SoleToJointStates.AutomatedChecksPassed);
        }

        protected override void SetUpStateActions()
        {
            Configure(SoleToJointStates.SelectTenants, Assignment.Create("tenants"), null);
            Configure(SoleToJointStates.AutomatedChecksFailed, Assignment.Create("tenants"), AddIncomingTenantId);
            Configure(SoleToJointStates.AutomatedChecksPassed, Assignment.Create("tenants"), AddIncomingTenantId);
        }
    }
}
