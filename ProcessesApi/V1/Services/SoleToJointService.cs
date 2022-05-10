using ProcessesApi.V1.Domain;
using ProcessesApi.V1.Services.Interfaces;
using Stateless;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Hackney.Core.Sns;
using ProcessesApi.V1.Factories;
using ProcessesApi.V1.Helpers;
using ProcessesApi.V1.Services.Exceptions;

namespace ProcessesApi.V1.Services
{
    public class SoleToJointService : ProcessService, ISoleToJointService
    {
        private readonly ISoleToJointAutomatedEligibilityChecksHelper _automatedcheckshelper;

        public SoleToJointService(ISnsFactory snsFactory, ISnsGateway snsGateway, ISoleToJointAutomatedEligibilityChecksHelper automatedChecksHelper)
            : base(snsFactory, snsGateway)
        {
            _snsFactory = snsFactory;
            _snsGateway = snsGateway;
            _automatedcheckshelper = automatedChecksHelper;
            _permittedTriggersType = typeof(SoleToJointPermittedTriggers);
        }

        #region Internal Transitions

        private async Task CheckAutomatedEligibility(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as UpdateProcessState;
            var formData = processRequest.FormData;
            SoleToJointHelpers.ValidateFormData(formData, new List<string>() { SoleToJointFormDataKeys.IncomingTenantId, SoleToJointFormDataKeys.TenantId });

            var isEligible = await _automatedcheckshelper.CheckAutomatedEligibility(_process.TargetId,
                                                                     Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.IncomingTenantId].ToString()),
                                                                     Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.TenantId].ToString()))
                                                                     .ConfigureAwait(false);

            processRequest.Trigger = isEligible ? SoleToJointInternalTriggers.EligibiltyPassed : SoleToJointInternalTriggers.EligibiltyFailed;

            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        private async Task CheckManualEligibility(StateMachine<string, string>.Transition transition)
        {
            var processRequest = transition.Parameters[0] as UpdateProcessState;
            processRequest.ValidateManualCheck(SoleToJointInternalTriggers.ManualEligibilityPassed,
                                               SoleToJointInternalTriggers.ManualEligibilityFailed,
                                               (SoleToJointFormDataKeys.BR11, "true"),
                                               (SoleToJointFormDataKeys.BR12, "false"),
                                               (SoleToJointFormDataKeys.BR13, "false"),
                                               (SoleToJointFormDataKeys.BR15, "false"),
                                               (SoleToJointFormDataKeys.BR16, "false"),
                                               (SoleToJointFormDataKeys.BR7, "false"),
                                               (SoleToJointFormDataKeys.BR8, "false"));
            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        private async Task CheckTenancyBreach(StateMachine<string, string>.Transition transition)
        {

            var processRequest = transition.Parameters[0] as UpdateProcessState;
            processRequest.ValidateManualCheck(SoleToJointInternalTriggers.BreachChecksPassed,
                                               SoleToJointInternalTriggers.BreachChecksFailed,
                                               (SoleToJointFormDataKeys.BR5, "false"),
                                               (SoleToJointFormDataKeys.BR10, "false"),
                                               (SoleToJointFormDataKeys.BR17, "false"),
                                               (SoleToJointFormDataKeys.BR18, "false"));
            await TriggerStateMachine(processRequest).ConfigureAwait(false);
        }

        #endregion

        #region State Transition Actions

        private void AddIncomingTenantId(UpdateProcessState processRequest)
        {
            SoleToJointHelpers.ValidateFormData(processRequest.FormData, new List<string>() { SoleToJointFormDataKeys.IncomingTenantId });

            //TODO: When doing a POST request from the FE they should created a relatedEntities object with all neccesary values
            // Once Frontend work is completed the IF statement below should be removed.
            if (_process.RelatedEntities == null)
                _process.RelatedEntities = new List<Guid>();

            _process.RelatedEntities.Add(Guid.Parse(processRequest.FormData[SoleToJointFormDataKeys.IncomingTenantId].ToString()));
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

        private async Task OnTenancyBreachCheckFailed(UpdateProcessState processRequest)
        {
            await PublishProcessClosedEvent("Tenancy Breach Check failed - process closed.");
        }

        private async Task OnRequestDocumentsAppointment(UpdateProcessState processRequest)
        {
            SoleToJointHelpers.ValidateFormData(processRequest.FormData, new List<string>() { SoleToJointFormDataKeys.AppointmentDateTime });
            var appointmentDetails = processRequest.FormData[SoleToJointFormDataKeys.AppointmentDateTime];

            if (DateTime.TryParse(appointmentDetails.ToString(), out DateTime appointmentDateTime))
            {
                await PublishProcessUpdatedEvent($"Supporting Documents requested via an office appointment on {appointmentDateTime.ToString("dd/MM/yyyy hh:mm tt")}");
            }
            else
            {
                throw new FormDataFormatException("appointment datetime", appointmentDetails);
            }
        }

        #endregion

        protected override void SetUpStates()
        {
            _machine.Configure(SharedProcessStates.ApplicationInitialised)
                    .Permit(SharedInternalTriggers.StartApplication, SoleToJointStates.SelectTenants);
            _machine.Configure(SoleToJointStates.SelectTenants)
                    .InternalTransitionAsync(SoleToJointPermittedTriggers.CheckAutomatedEligibility, async (x) => await CheckAutomatedEligibility(x).ConfigureAwait(false))
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
            _machine.Configure(SoleToJointStates.BreachChecksPassed)
                    .Permit(SoleToJointPermittedTriggers.RequestDocumentsAppointment, SoleToJointStates.DocumentsRequestedAppointment);
        }

        protected override void SetUpStateActions()
        {
            ConfigureAsync(SoleToJointStates.SelectTenants, Assignment.Create("tenants"), (x) => PublishProcessStartedEvent());
            ConfigureAsync(SoleToJointStates.AutomatedChecksFailed, Assignment.Create("tenants"), OnAutomatedCheckFailed);
            Configure(SoleToJointStates.AutomatedChecksPassed, Assignment.Create("tenants"), AddIncomingTenantId);
            Configure(SoleToJointStates.ProcessCancelled, Assignment.Create("tenants"));
            Configure(SoleToJointStates.ManualChecksPassed, Assignment.Create("tenants"));
            ConfigureAsync(SoleToJointStates.ManualChecksFailed, Assignment.Create("tenants"), OnManualCheckFailed);
            ConfigureAsync(SoleToJointStates.BreachChecksPassed, Assignment.Create("tenants"));
            ConfigureAsync(SoleToJointStates.BreachChecksFailed, Assignment.Create("tenants"), OnTenancyBreachCheckFailed);
            ConfigureAsync(SoleToJointStates.DocumentsRequestedAppointment, Assignment.Create("tenants"), OnRequestDocumentsAppointment);
        }
    }
}
